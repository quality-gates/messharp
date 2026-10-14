using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class LiteralControlFlow
{
    public static IEnumerable<SyntaxNode> Children(SyntaxNode node)
    {
        var skipped = Skipped(node).ToHashSet();
        return node.ChildNodes().Where(child => !skipped.Contains(child));
    }

    private static IEnumerable<SyntaxNode> Skipped(SyntaxNode node) => node switch
    {
        ConstructorDeclarationSyntax constructor => ConstructorBodies.Inert(constructor),
        IfStatementSyntax statement when Boolean(statement.Condition) is { } condition =>
            Optional(condition ? statement.Else : statement.Statement),
        WhileStatementSyntax statement when Boolean(statement.Condition) == false => [statement.Statement],
        ForStatementSyntax statement when Boolean(statement.Condition) == false =>
            statement.Incrementors.Cast<SyntaxNode>().Append(statement.Statement),
        ConditionalExpressionSyntax choice when Boolean(choice.Condition) is { } condition =>
            [condition ? choice.WhenFalse : choice.WhenTrue],
        _ => [],
    };

    private static IEnumerable<SyntaxNode> Optional(SyntaxNode? node) => node is null ? [] : [node];

    public static bool? Boolean(ExpressionSyntax? expression) => expression switch
    {
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.TrueLiteralExpression) => true,
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.FalseLiteralExpression) => false,
        ParenthesizedExpressionSyntax parenthesized => Boolean(parenthesized.Expression),
        _ => null,
    };
}
