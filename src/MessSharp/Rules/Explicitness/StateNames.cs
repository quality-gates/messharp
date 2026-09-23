using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Resolves expressions to the names of one kind of state: class members
/// (bare or qualified by <c>this</c>, <c>base</c> or the class name, also
/// with type arguments as in <c>Cache&lt;T&gt;.value</c>) or
/// method parameters (bare only).
/// </summary>
internal sealed class StateNames
{
    private readonly IReadOnlySet<string> _names;
    private readonly Func<ExpressionSyntax, bool> _isOwnQualifier;
    private readonly bool _canBeShadowed;

    private StateNames(IReadOnlySet<string> names, Func<ExpressionSyntax, bool> isOwnQualifier, bool canBeShadowed)
    {
        _names = names;
        _isOwnQualifier = isOwnQualifier;
        _canBeShadowed = canBeShadowed;
    }

    public static StateNames StaticMembers(IReadOnlySet<string> names, string className) =>
        new(names, q => q is SimpleNameSyntax type && type.Identifier.Text == className, canBeShadowed: true);

    public static StateNames InstanceMembers(IReadOnlySet<string> names) =>
        new(names, q => q is ThisExpressionSyntax or BaseExpressionSyntax, canBeShadowed: true);

    public static StateNames Parameters(IReadOnlySet<string> names) =>
        new(names, _ => false, canBeShadowed: false);

    public string? Resolve(ExpressionSyntax expr) => expr switch
    {
        IdentifierNameSyntax id when IsStandaloneReference(id) => id.Identifier.Text,
        MemberAccessExpressionSyntax ma when _isOwnQualifier(ma.Expression) && _names.Contains(ma.Name.Identifier.Text)
            => ma.Name.Identifier.Text,
        _ => null,
    };

    private bool IsStandaloneReference(IdentifierNameSyntax id)
    {
        var name = id.Identifier.Text;
        return _names.Contains(name)
            && !IsQualifiedOrKeyName(id)
            && !IsObjectInitializerKey(id)
            && !SyntaxFacts.IsInTypeOnlyContext(id)
            && !IsInNameOf(id)
            && !(_canBeShadowed && LocalShadowing.IsShadowedByLocal(id, name));
    }

    /// <summary>
    /// True for the member part of <c>a.x</c> or <c>a?.x</c>, and for the name
    /// in a named argument or anonymous-object member.
    /// </summary>
    private static bool IsQualifiedOrKeyName(IdentifierNameSyntax id) => id.Parent switch
    {
        MemberAccessExpressionSyntax ma => ma.Name == id,
        MemberBindingExpressionSyntax => true,
        QualifiedNameSyntax => true,
        NameColonSyntax => true,
        NameEqualsSyntax => true,
        _ => false,
    };

    /// <summary>True for <c>X</c> in <c>new T { X = 1 }</c>: it names a member of the new object.</summary>
    private static bool IsObjectInitializerKey(IdentifierNameSyntax id) =>
        id.Parent is AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax } assignment
        && assignment.Left == id;

    private static bool IsInNameOf(IdentifierNameSyntax id) =>
        id.Ancestors().OfType<InvocationExpressionSyntax>()
            .Any(i => i.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" });
}
