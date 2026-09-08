using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Metrics;

/// <summary>
/// NPath contributions from expressions: boolean operators, null-coalescing,
/// and additional switch-expression arms.
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
        {
            if (node is SwitchExpressionSyntax switchExpression)
                count = NPathArithmetic.Add(count, Math.Max(0, switchExpression.Arms.Count - 1));
            else if (node is BinaryExpressionSyntax bin && IsBooleanOperator(bin))
                count = NPathArithmetic.Add(count, 1);
        }
        return count;
    }

    private static bool IsBooleanOperator(BinaryExpressionSyntax bin) =>
        bin.IsKind(SyntaxKind.LogicalAndExpression)
        || bin.IsKind(SyntaxKind.LogicalOrExpression)
        || bin.IsKind(SyntaxKind.CoalesceExpression);
}
