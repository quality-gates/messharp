using Microsoft.CodeAnalysis;

namespace MessSharp.Metrics;

/// <summary>
/// Lines-of-code metrics: raw span and effective (non-blank, non-comment) LOC.
/// </summary>
internal static class LinesOfCodeMetrics
{
    /// <summary>Lines of code spanned by a node (inclusive, 1-based).</summary>
    internal static int LinesOfCode(SyntaxNode node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    /// <summary>
    /// Effective LOC: lines that contain code, skipping blank lines and
    /// comment-only lines.
    /// </summary>
    internal static int EffectiveLinesOfCode(SyntaxNode node, string source)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        int first = span.StartLinePosition.Line;
        int last = span.EndLinePosition.Line;

        var lines = new HashSet<int>();
        foreach (var token in node.DescendantTokens())
        {
            var tokenSpan = token.GetLocation().GetLineSpan();
            int start = tokenSpan.StartLinePosition.Line;
            int end = tokenSpan.EndLinePosition.Line;
            for (int l = start; l <= end; l++)
            {
                if (l >= first && l <= last)
                    lines.Add(l);
            }
        }
        return lines.Count;
    }
}
