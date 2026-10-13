using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class BodyWalk
{
    public static void Run(SyntaxNode body, IReadOnlyList<IBodyVisitor> visitors)
    {
        foreach (var node in Nodes(body))
        {
            foreach (var visitor in visitors)
                visitor.Visit(node);
        }
    }

    public static IEnumerable<SyntaxNode> Nodes(SyntaxNode body) => ReachableNodes(body, body);

    private static IEnumerable<SyntaxNode> ReachableNodes(SyntaxNode node, SyntaxNode root)
    {
        yield return node;
        if (!CanDescend(node, root)) yield break;
        foreach (var child in LiteralControlFlow.Children(node))
            foreach (var descendant in ReachableNodes(child, root))
                yield return descendant;
    }

    private static bool CanDescend(SyntaxNode node, SyntaxNode root) =>
        node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax
        && (node == root || node is not MemberDeclarationSyntax || node is GlobalStatementSyntax);

}
