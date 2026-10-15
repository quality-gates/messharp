using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class MutationTargets
{
    public static IEnumerable<(ExpressionSyntax Target, bool IsPureWrite)> Find(SyntaxNode node) => node switch
    {
        AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax } => [],
        AssignmentExpressionSyntax assignment => Assigned(assignment.Left)
            .Select(target => (target, assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))),
        PrefixUnaryExpressionSyntax prefix when IsIncrementOrDecrement(prefix.Kind()) => [(prefix.Operand, false)],
        PostfixUnaryExpressionSyntax postfix when IsIncrementOrDecrement(postfix.Kind()) => [(postfix.Operand, false)],
        ArgumentSyntax { RefKindKeyword.RawKind: (int)SyntaxKind.OutKeyword, Expression: not DeclarationExpressionSyntax } argument =>
            [(argument.Expression, true)],
        _ => [],
    };

    private static IEnumerable<ExpressionSyntax> Assigned(ExpressionSyntax left) =>
        left is TupleExpressionSyntax tuple ? tuple.Arguments.SelectMany(argument => Assigned(argument.Expression)) : [left];

    private static bool IsIncrementOrDecrement(SyntaxKind kind) =>
        kind is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression
            or SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression;
}
