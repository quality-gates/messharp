using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class BodyWalk
{
    /// <summary>Walks the body once, showing each node to every visitor.</summary>
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
        foreach (var child in ReachableChildren(node))
            foreach (var descendant in ReachableNodes(child, root))
                yield return descendant;
    }

    private static bool CanDescend(SyntaxNode node, SyntaxNode root) =>
        node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax
        && (node == root || node is not MemberDeclarationSyntax || node is GlobalStatementSyntax);

    private static IEnumerable<SyntaxNode> ReachableChildren(SyntaxNode node)
    {
        if (node is IfStatementSyntax statement && BoolLiteral(statement.Condition) is { } condition)
        {
            SyntaxNode? skipped = condition ? statement.Else : statement.Statement;
            return node.ChildNodes().Where(child => child != skipped);
        }
        return node.ChildNodes();
    }

    private static bool? BoolLiteral(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.TrueLiteralExpression) => true,
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.FalseLiteralExpression) => false,
        ParenthesizedExpressionSyntax parenthesized => BoolLiteral(parenthesized.Expression),
        PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.LogicalNotExpression) =>
            BoolLiteral(prefix.Operand) is { } value ? !value : null,
        _ => null,
    };
}
