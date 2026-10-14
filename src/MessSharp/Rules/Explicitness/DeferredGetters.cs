using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The values a same-class property read can return: every return of its non-virtual getter, evaluated
/// by that getter. A virtual, abstract or override property can be replaced, so it stays opaque.
/// </summary>
internal sealed class DeferredGetters(CallableOwner owner, IReadOnlyList<CallableOwner> owners)
{
    public IReadOnlyList<DeferredSource>? Values(ExpressionSyntax reference)
    {
        if (Name(reference) is not { } name || owner.Method.Class is not { } cls) return null;
        var property = cls.Node.ChildNodes().OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(candidate => candidate.Identifier.Text == name);
        if (property is null) return null;
        return property.Modifiers.Any(IsReplaceable) ? [new DeferredSource.Opaque()] : Returns(cls, name);
    }

    /// <summary>Every return of the property's getter, evaluated by it; opaque when the getter has no body.</summary>
    private IReadOnlyList<DeferredSource> Returns(ClassModel cls, string name)
    {
        var getter = owners.FirstOrDefault(candidate => candidate.Parent is null && candidate.Method.Class == cls
            && candidate.Method.Name == "get_" + name);
        if (getter?.Body is not { } body) return [new DeferredSource.Opaque()];
        return CallableSyntax.Returns(body).Select(value => (DeferredSource)new DeferredSource.Value(value, getter)).ToList();
    }

    private string? Name(ExpressionSyntax reference) => reference switch
    {
        IdentifierNameSyntax name => name.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } member => member.Name.Identifier.Text,
        MemberAccessExpressionSyntax member when member.Expression.ToString() == owner.Method.Class?.Name =>
            member.Name.Identifier.Text,
        _ => null,
    };

    private static bool IsReplaceable(SyntaxToken modifier) => modifier.IsKind(SyntaxKind.VirtualKeyword)
        || modifier.IsKind(SyntaxKind.AbstractKeyword) || modifier.IsKind(SyntaxKind.OverrideKeyword)
        || modifier.IsKind(SyntaxKind.ExternKeyword);
}
