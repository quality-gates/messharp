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
    private readonly HashSet<string> _readonlyCollections = new(StringComparer.Ordinal);
    private readonly HashSet<string> _instance = new(StringComparer.Ordinal);

    /// <summary>All static state, also readonly fields whose object can change.</summary>
    public IReadOnlySet<string> Statics => _statics;

    /// <summary>Static state that changes after type initialization.</summary>
    public IReadOnlySet<string> MutableStatics => _mutableStatics;

    public IReadOnlySet<string> Instance => _instance;

    /// <summary>The class's state, gathered once per analysis.</summary>
    public static ClassState For(RuleContext ctx, ClassModel cls) =>
        ctx.Facts.Get(cls, () => Gather(ctx, cls));

    private static ClassState Gather(RuleContext ctx, ClassModel cls)
    {
        var state = new ClassState();
        var parts = ctx.Partials.PartsInOtherFiles(cls).Prepend(cls).ToList();
        foreach (var part in parts)
            state.AddPart(part.Node);
        var writes = new StateAccessCollector(
            StateNames.StaticMembers(state._readonlyCollections, cls.Name).Resolve, state._readonlyCollections);
        foreach (var part in parts)
            foreach (var node in part.Node.DescendantNodes(n => n is not ConstructorDeclarationSyntax constructor
                || !constructor.Modifiers.Any(SyntaxKind.StaticKeyword)))
                writes.Visit(node);
        state._mutableStatics.UnionWith(writes.Accesses().Where(a => a.Kind != AccessKind.Read).Select(a => a.Name));
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
        else if (StorageTypes.IsMutable(field.Declaration.Type.ToString()))
            _readonlyCollections.UnionWith(names);
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
        else if (StorageTypes.IsMutable(property.Type.ToString()))
            _readonlyCollections.Add(name);
    }

    private static bool HasSetter(PropertyDeclarationSyntax property) =>
        property.AccessorList?.Accessors.Any(a => a.Keyword.IsKind(SyntaxKind.SetKeyword)) == true;
}
