using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal abstract record StorageStep(SyntaxNode Node, int Position)
{
    internal sealed record Bound(StorageBinding Binding) : StorageStep(Binding.Node, Binding.Position);
    internal sealed record Executed(SyntaxNode Operation) : StorageStep(Operation, Operation.Span.End);
}

internal static class StorageSteps
{
    public static IReadOnlyList<StorageStep> Gather(SyntaxNode body, IReadOnlyList<StorageBinding> bindings) =>
        bindings.Select(binding => (StorageStep)new StorageStep.Bound(binding))
            .Concat(BodyWalk.Nodes(body).Where(Executes).Select(node => new StorageStep.Executed(node)))
            .OrderBy(step => step.Position).ToList();

    private static bool Executes(SyntaxNode node) => node is InvocationExpressionSyntax
        or AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax or ElementAccessExpressionSyntax };
}
