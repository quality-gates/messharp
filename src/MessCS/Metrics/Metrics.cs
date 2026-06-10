using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Metrics;

/// <summary>
/// Code metrics facade: cyclomatic complexity, NPath complexity, and lines of
/// code. Delegates to focused helper classes in this namespace.
/// </summary>
public static class Metrics
{
    /// <summary>
    /// Cyclomatic complexity: base 1 + 1 per decision point.
    /// </summary>
    public static int CyclomaticComplexity(BlockSyntax? body) =>
        CyclomaticMetrics.Compute(body);

    /// <summary>
    /// NPath complexity using Nejmeh's algorithm.
    /// </summary>
    public static int NPathComplexity(BlockSyntax? body) =>
        NPathMetrics.Compute(body);

    /// <summary>
    /// Lines of code spanned by a node (inclusive, 1-based).
    /// </summary>
    public static int LinesOfCode(Microsoft.CodeAnalysis.SyntaxNode node) =>
        LinesOfCodeMetrics.LinesOfCode(node);

    /// <summary>
    /// Effective LOC: lines that contain code, skipping blank lines and
    /// comment-only lines.
    /// </summary>
    public static int EffectiveLinesOfCode(Microsoft.CodeAnalysis.SyntaxNode node, string source) =>
        LinesOfCodeMetrics.EffectiveLinesOfCode(node, source);
}
