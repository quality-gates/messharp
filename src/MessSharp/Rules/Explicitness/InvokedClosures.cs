using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>An assignment inside a closure, with the statements of the enclosing body that run the closure.</summary>
internal sealed record ClosureWrite(IReadOnlyList<InvocationExpressionSyntax> Sites, ExpressionSyntax Value);

/// <summary>
/// Closures that a body only ever runs by invoking a local that holds them, as a statement of the body itself.
/// Such a closure's one unconditional write to a local of the body happens exactly where the body invokes it, so
/// the body can order it like an assignment there instead of treating it as written at an unknown time.
/// </summary>
internal static class InvokedClosures
{
    /// <summary>The write <paramref name="assignment"/> does when <paramref name="body"/> runs its closure, or null when that is not known.</summary>
    public static ClosureWrite? Find(SyntaxNode body, AssignmentExpressionSyntax assignment) =>
        Closure(body, assignment) is { } closure && Holder(closure) is { } holder && Sites(body, holder) is { } sites
            ? new ClosureWrite(sites, assignment.Right) : null;

    /// <summary>The closure that unconditionally rebinds a local of <paramref name="body"/> with a value of its own scope's names.</summary>
    private static AnonymousFunctionExpressionSyntax? Closure(SyntaxNode body, AssignmentExpressionSyntax assignment)
    {
        if (!RebindsLocalOf(body, assignment) || NearestFunction(body, assignment) is not { } closure) return null;
        return RunsUnconditionally(closure, assignment) && !UsesClosureNames(closure, assignment.Right) ? closure : null;
    }

    private static bool RebindsLocalOf(SyntaxNode body, AssignmentExpressionSyntax assignment) =>
        assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) && assignment.Left is IdentifierNameSyntax target
        && ExpressionTypes.Binding(target) is { } declared && body.Span.Contains(declared.Span);

    private static VariableDeclaratorSyntax? Holder(AnonymousFunctionExpressionSyntax closure) =>
        closure.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax holder } ? holder : null;

    /// <summary>The closure directly inside <paramref name="body"/> that contains <paramref name="node"/>, when no other function is between.</summary>
    private static AnonymousFunctionExpressionSyntax? NearestFunction(SyntaxNode body, SyntaxNode node)
    {
        var functions = node.Ancestors().TakeWhile(parent => parent != body)
            .Where(parent => parent is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax).ToList();
        return functions is [AnonymousFunctionExpressionSyntax closure] ? closure : null;
    }

    private static bool RunsUnconditionally(AnonymousFunctionExpressionSyntax closure, AssignmentExpressionSyntax assignment) =>
        closure.ExpressionBody == assignment
        || assignment.Parent is ExpressionStatementSyntax { Parent: BlockSyntax block } && closure.Block == block;

    private static bool UsesClosureNames(AnonymousFunctionExpressionSyntax closure, ExpressionSyntax value)
    {
        var declared = closure.DescendantNodes().OfType<VariableDeclaratorSyntax>().Select(variable => variable.Identifier.Text)
            .Concat(CallableOwner.Parameters(closure).Select(parameter => parameter.Identifier.Text)).ToHashSet();
        return value.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(name => declared.Contains(name.Identifier.Text));
    }

    /// <summary>The invocation statements of <paramref name="body"/>, when every use of the local holding the closure is one.</summary>
    private static IReadOnlyList<InvocationExpressionSyntax>? Sites(SyntaxNode body, VariableDeclaratorSyntax holder)
    {
        var sites = new List<InvocationExpressionSyntax>();
        foreach (var use in body.DescendantNodes().OfType<IdentifierNameSyntax>()
                     .Where(name => name.Identifier.Text == holder.Identifier.Text && ExpressionTypes.Binding(name) == holder))
        {
            if (use.Parent is not InvocationExpressionSyntax { Parent: ExpressionStatementSyntax } call || call.Expression != use
                || NearestFunction(body, use) is not null) return null;
            sites.Add(call);
        }
        return sites;
    }
}
