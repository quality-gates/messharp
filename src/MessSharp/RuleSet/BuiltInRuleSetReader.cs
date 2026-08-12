namespace MessSharp.RuleSet;

internal static class BuiltInRuleSetReader
{
    internal static byte[]? Read(string filename)
    {
        var path = FindFile(filename);
        if (path != null) return File.ReadAllBytes(path);

        using var stream = typeof(BuiltInRuleSetReader).Assembly
            .GetManifestResourceStream($"MessSharp.rulesets.{filename}");
        if (stream == null) return null;

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string? FindFile(string filename)
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "rulesets", filename);
        if (File.Exists(candidate)) return candidate;

        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var ruleSetPath = Path.Combine(dir, "rulesets", filename);
            if (File.Exists(ruleSetPath)) return ruleSetPath;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == null) break;
            dir = parent;
        }
        return null;
    }
}
