namespace MessSharp.RuleSet;

/// <summary>
/// Resolves ruleset file paths from built-in names or explicit paths.
/// Extracted from Loader to reduce that class's coupling and method count.
/// </summary>
internal static class LoaderFileResolver
{
    /// <summary>Searches for a built-in ruleset file next to the exe and up the repo tree.</summary>
    internal static string? FindBuiltinFile(string filename)
    {
        // 1. Next to the executable (output dir)
        var candidate = Path.Combine(AppContext.BaseDirectory, "rulesets", filename);
        if (File.Exists(candidate)) return candidate;

        // 2. Repo-relative fallback (for running from source)
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var p = Path.Combine(dir, "rulesets", filename);
            if (File.Exists(p)) return p;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == null) break;
            dir = parent;
        }
        return null;
    }
}
