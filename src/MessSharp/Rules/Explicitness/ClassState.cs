using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The data members that a class declares across all its partial parts.
/// Constants are not state. Static members are fields and auto-properties;
/// instance members are fields, properties and primary constructor parameters.
/// </summary>
internal sealed class ClassState
{
    private readonly HashSet<string> _statics = new(StringComparer.Ordinal);
    private readonly HashSet<string> _mutableStatics = new(StringComparer.Ordinal);
    private readonly HashSet<string> _instance = new(StringComparer.Ordinal);

    /// <summary>All static state, also readonly fields whose object can change.</summary>
    public IReadOnlySet<string> Statics => _statics;

    /// <summary>Static state that can get a new value after type initialization.</summary>
    public IReadOnlySet<string> MutableStatics => _mutableStatics;

    public IReadOnlySet<string> Instance => _instance;

    public static ClassState Gather(RuleContext ctx, ClassModel cls)
    {
        var state = new ClassState();
        foreach (var part in ctx.Partials.PartsInOtherFiles(cls).Prepend(cls))
            state.AddPart(part.Node);
        return state;
    }

    private void AddPart(BaseTypeDeclarationSyntax node)
    {
        if (node is TypeDeclarationSyntax { ParameterList: { } primary })
            _instance.UnionWith(primary.Parameters.Select(p => p.Identifier.Text));

        foreach (var member in ModelBuilderHelpers.MembersOf(node))
        {
            if (member is FieldDeclarationSyntax field)
                AddField(field);
            else if (member is PropertyDeclarationSyntax property)
                AddProperty(property);
        }
    }

    private void AddField(FieldDeclarationSyntax field)
    {
        var mods = field.Modifiers;
        if (mods.Any(SyntaxKind.ConstKeyword)) return;
        var names = field.Declaration.Variables.Select(v => v.Identifier.Text);
        if (!mods.Any(SyntaxKind.StaticKeyword))
        {
            _instance.UnionWith(names);
            return;
        }
        _statics.UnionWith(names);
        if (!mods.Any(SyntaxKind.ReadOnlyKeyword))
            _mutableStatics.UnionWith(names);
    }

    private void AddProperty(PropertyDeclarationSyntax property)
    {
        var name = property.Identifier.Text;
        if (!property.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            _instance.Add(name);
            return;
        }
        if (!ModelBuilderHelpers.IsAutoProperty(property)) return;
        _statics.Add(name);
        if (HasSetter(property))
            _mutableStatics.Add(name);
    }

    private static bool HasSetter(PropertyDeclarationSyntax property) =>
        property.AccessorList?.Accessors.Any(a => a.Keyword.IsKind(SyntaxKind.SetKeyword)) == true;
}
