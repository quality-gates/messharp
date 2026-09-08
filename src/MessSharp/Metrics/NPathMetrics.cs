using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Metrics;

/// <summary>
/// NPath complexity using Nejmeh's algorithm, as implemented by pdepend's
/// NPathComplexityAnalyzer. Values pinned to phpmd 2.15.0 reference output.
/// </summary>
internal static class NPathMetrics
{
    internal static int Compute(SyntaxNode? body)
    {
        if (body == null) return 1;
        if (body is BlockSyntax blk) return NPathStatementMetrics.Compute(blk.Statements);
        if (body is ExpressionSyntax expr) return NPathExpressionMetrics.Compute(expr);
        return 1;
    }
}
