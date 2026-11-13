using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The value a closure reads from a local of its enclosing body when that body only ever runs the closure by
/// calling it directly: the value the local holds where each call happens, not every value it ever holds.
/// </summary>
internal static class InvokedCaptures
{
    /// <summary>The values <paramref name="declaration"/> holds at the calls of <paramref name="owner"/>, or null when they are not known.</summary>
    public static IReadOnlyList<DeferredSource>? Values(CallableOwner owner, SyntaxNode declaration,
        IReadOnlyList<CallableOwner> owners)
    {
        if (owner.Parent is not { Body: { } outer } parent || owner.Body is not { } inner || !IsOuterLocal(declaration, outer, inner)) return null;
        var declarations = new DeferredDeclarations(parent, owners);
        if (inner.DescendantNodes().Any(node => declarations.IsWrittenBy(node, declaration))
            || Sites(owner, outer) is not { Count: > 0 } sites) return null;
        var flow = new DeclarationFlow(outer, declarations);
        var values = sites.SelectMany(site => flow.Reaching(declaration, site)).ToList();
        values.AddRange(new DeferredWriters(parent, outer, owners, declarations).Nested(declaration));
        return values;
    }

    private static bool IsOuterLocal(SyntaxNode declaration, SyntaxNode outer, SyntaxNode inner) =>
        declaration is VariableDeclaratorSyntax && !DeferredDeclarations.IsShared(declaration)
        && outer.Span.Contains(declaration.Span) && !inner.Span.Contains(declaration.Span);

    /// <summary>The direct calls of the closure, when every use of the name that holds it is one made outside any closure.</summary>
    private static IReadOnlyList<InvocationExpressionSyntax>? Sites(CallableOwner owner, SyntaxNode outer)
    {
        if (HolderName(owner.Method.Node) is not { } name) return null;
        var sites = new List<InvocationExpressionSyntax>();
        foreach (var use in outer.DescendantNodes().OfType<IdentifierNameSyntax>().Where(use => use.Identifier.Text == name))
        {
            if (use.Parent is not InvocationExpressionSyntax call || call.Expression != use || IsInsideClosure(outer, use)) return null;
            sites.Add(call);
        }
        return sites;
    }

    private static string? HolderName(SyntaxNode closure) => closure switch
    {
        LocalFunctionStatementSyntax local => local.Identifier.Text,
        AnonymousFunctionExpressionSyntax { Parent: EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax holder } } =>
            holder.Identifier.Text,
        _ => null,
    };

    private static bool IsInsideClosure(SyntaxNode outer, SyntaxNode node) => node.Ancestors().TakeWhile(parent => parent != outer)
        .Any(parent => parent is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
}
