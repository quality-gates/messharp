using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Metrics;

/// <summary>
/// NPath contributions from expressions: boolean operators, null-coalescing,
/// ternary conditionals, and additional switch-expression arms.
/// </summary>
internal static class NPathExpressionMetrics
{
    internal static int Compute(ExpressionSyntax expression) =>
        NPathArithmetic.Add(1, Complexity(expression));

    internal static int Complexity(ExpressionSyntax? expression)
    {
        if (expression == null) return 0;

        int count = 0;
        foreach (var node in expression.DescendantNodesAndSelf())
            count = NPathArithmetic.Add(count, NodeComplexity(node));
        return count;
    }

    private static int NodeComplexity(SyntaxNode node)
    {
        if (node is SwitchExpressionSyntax switchExpression)
            return Math.Max(0, switchExpression.Arms.Count - 1);
        if (node is ConditionalExpressionSyntax)
            return 2;
        if (node is BinaryExpressionSyntax bin && IsBooleanOperator(bin))
            return 1;
        return 0;
    }

    private static bool IsBooleanOperator(BinaryExpressionSyntax bin) =>
        bin.IsKind(SyntaxKind.LogicalAndExpression)
        || bin.IsKind(SyntaxKind.LogicalOrExpression)
        || bin.IsKind(SyntaxKind.CoalesceExpression);
}
