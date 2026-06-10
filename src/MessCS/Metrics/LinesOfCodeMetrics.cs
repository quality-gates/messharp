using Microsoft.CodeAnalysis;

namespace MessCS.Metrics;

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
        var lines = source.Split('\n');
        int count = 0;
        bool inBlock = false;
        for (int i = 0; i < lines.Length; i++)
        {
            bool hasCode;
            (hasCode, inBlock) = LineHasCode(lines[i], inBlock);
            if (i >= first && i <= last && hasCode)
                count++;
        }
        return count;
    }

    private static (bool hasCode, bool blockAfter) LineHasCode(string line, bool inBlock)
    {
        bool hasCode = false;
        int i = 0;
        while (i < line.Length)
        {
            if (inBlock)
            {
                i = AdvanceBlockComment(line, i, ref inBlock);
                continue;
            }
            var (stop, consumed, newBlock) = ScanChar(line, i, hasCode);
            if (stop) return (hasCode, false);
            inBlock = newBlock;
            if (consumed > 0) { i += consumed; continue; }
            if (line[i] != ' ' && line[i] != '\t' && line[i] != '\r')
                hasCode = true;
            i++;
        }
        return (hasCode, inBlock);
    }

    private static int AdvanceBlockComment(string line, int i, ref bool inBlock)
    {
        if (line[i] == '*' && i + 1 < line.Length && line[i + 1] == '/')
        {
            inBlock = false;
            return i + 2;
        }
        return i + 1;
    }

    // Returns (stop, extraConsumed, newInBlock).
    // stop=true means return (hasCode, false) immediately (line comment found).
    private static (bool stop, int consumed, bool newInBlock) ScanChar(string line, int i, bool hasCode)
    {
        if (line[i] != '/' || i + 1 >= line.Length)
            return (false, 0, false);
        if (line[i + 1] == '/') return (true, 0, false);
        if (line[i + 1] == '*') return (false, 2, true);
        return (false, 0, false);
    }
}
