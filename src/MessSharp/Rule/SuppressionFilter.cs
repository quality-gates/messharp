using MessSharp.Model;

namespace MessSharp.Rule;

/// <summary>
/// Drops violations whose flagged declaration, or any enclosing declaration or
/// the file itself, carries a suppression recognised by SuppressionMatcher.
/// </summary>
internal static class SuppressionFilter
{
    public static List<Violation> Filter(List<Violation> violations, SourceFile file)
    {
        if (violations.Count == 0) return violations;
        return violations.Where(v => !IsSuppressed(v, file)).ToList();
    }

    private static bool IsSuppressed(Violation v, SourceFile file)
    {
        if (SuppressionMatcher.IsNodeSuppressed(file.Root, v.Rule)) return true;
        return v.Node != null
            && v.Node.AncestorsAndSelf().Any(n => SuppressionMatcher.IsNodeSuppressed(n, v.Rule));
    }
}
