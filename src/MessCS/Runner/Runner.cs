using MessCS.Model;
using MessCS.Report;
using MessCS.Rule;
using RuleSetType = MessCS.Rule.RuleSet;

namespace MessCS.Runner;

public sealed class RunOptions
{
    public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RuleSetType> RuleSets { get; init; } = Array.Empty<RuleSetType>();
    public IReadOnlyList<string> Suffixes { get; init; } = new[] { ".cs" };
    public IReadOnlyList<string> Exclude { get; init; } = Array.Empty<string>();
    public bool IgnoreTests { get; init; }
}

public static class Runner
{
    public static Report.Report Run(RunOptions opts)
    {
        var suffixes = opts.Suffixes.Count > 0 ? opts.Suffixes : new[] { ".cs" };
        var files = Discover(opts.Paths, suffixes, opts.Exclude, opts.IgnoreTests);
        var report = new Report.Report();

        foreach (var path in files)
        {
            SourceFile sf;
            try { sf = ModelBuilder.ParseFile(path); }
            catch (Exception ex)
            {
                report.Errors.Add(new ProcessingError { File = path, Message = ex.Message });
                continue;
            }

            var violations = Engine.Analyze(sf, opts.RuleSets);
            report.Violations.AddRange(violations);
        }

        RuleContext.SortViolations(report.Violations);
        return report;
    }

    internal static List<string> Discover(
        IReadOnlyList<string> paths,
        IReadOnlyList<string> suffixes,
        IReadOnlyList<string> exclude,
        bool ignoreTests)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string p)
        {
            var abs = Path.GetFullPath(p);
            if (seen.Add(abs)) result.Add(p);
        }

        foreach (var p in paths)
        {
            if (Directory.Exists(p))
                WalkDir(p, suffixes, exclude, ignoreTests, Add);
            else if (File.Exists(p))
                Add(p);
            else
                // phpmd/messgo error out on a path that does not exist
                throw new FileNotFoundException($"no such file or directory: {p}");
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static void WalkDir(string root,
        IReadOnlyList<string> suffixes,
        IReadOnlyList<string> exclude,
        bool ignoreTests,
        Action<string> add)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            if (Directory.Exists(entry))
                WalkDirEntry(entry, suffixes, exclude, ignoreTests, add);
            else
                AddFileEntry(entry, suffixes, exclude, ignoreTests, add);
        }
    }

    private static void WalkDirEntry(string entry,
        IReadOnlyList<string> suffixes,
        IReadOnlyList<string> exclude,
        bool ignoreTests,
        Action<string> add)
    {
        var name = Path.GetFileName(entry);
        if (ShouldSkipDir(name)) return;
        if (ignoreTests && IsTestDir(name)) return;
        WalkDir(entry, suffixes, exclude, ignoreTests, add);
    }

    private static void AddFileEntry(string entry,
        IReadOnlyList<string> suffixes,
        IReadOnlyList<string> exclude,
        bool ignoreTests,
        Action<string> add)
    {
        if (!HasSuffix(entry, suffixes)) return;
        if (ignoreTests && IsTestFile(entry)) return;
        if (IsExcluded(entry, exclude)) return;
        add(entry);
    }

    private static bool ShouldSkipDir(string name) =>
        name is "bin" or "obj" or ".git" or "node_modules";

    private static bool IsTestDir(string name) =>
        name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase);

    private static bool IsTestFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSuffix(string path, IReadOnlyList<string> suffixes)
    {
        foreach (var s in suffixes)
            if (path.EndsWith(s, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsExcluded(string path, IReadOnlyList<string> exclude)
    {
        foreach (var e in exclude)
            if (!string.IsNullOrEmpty(e) && path.Contains(e, StringComparison.Ordinal)) return true;
        return false;
    }
}
