using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Metrics;

/// <summary>
/// Cyclomatic complexity metric. Base 1 + 1 per decision point.
/// Decision points: if, case label (not default), for, foreach, while,
/// do, catch, &amp;&amp;, ||, ??, ternary ?:.
/// Mirrors messgo's metrics package, values pinned to phpmd 2.15.0 output.
/// </summary>
internal static class CyclomaticMetrics
{
    private static readonly HashSet<Type> SimpleIncrementTypes = new()
    {
        typeof(IfStatementSyntax),
        typeof(ForStatementSyntax),
        typeof(ForEachStatementSyntax),
        typeof(WhileStatementSyntax),
        typeof(DoStatementSyntax),
        typeof(CatchClauseSyntax),
        typeof(ConditionalExpressionSyntax),
    };

    internal static int Compute(BlockSyntax? body)
    {
        if (body == null) return 1;
        int ccn = 1;
        foreach (var node in body.DescendantNodesAndSelf())
            ccn += Increment(node);
        return ccn;
    }

    private static int Increment(SyntaxNode node)
    {
        if (SimpleIncrementTypes.Contains(node.GetType()))
            return 1;

        if (node is SwitchSectionSyntax section)
            return section.Labels.Count(l => l is CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax);

        if (node is BinaryExpressionSyntax bin)
            return IsBooleanOp(bin) ? 1 : 0;

        return 0;
    }

    private static bool IsBooleanOp(BinaryExpressionSyntax bin) =>
        bin.IsKind(SyntaxKind.LogicalAndExpression)
        || bin.IsKind(SyntaxKind.LogicalOrExpression)
        || bin.IsKind(SyntaxKind.CoalesceExpression);
}
