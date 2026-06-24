namespace MessSharp.Runner;

public sealed class PhysicalFileDiscoverer : IFileDiscoverer
{
    public List<string> Discover(
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

    private void WalkDir(string root,
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

    private void WalkDirEntry(string entry,
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

    private void AddFileEntry(string entry,
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

    private bool ShouldSkipDir(string name) =>
        name is "bin" or "obj" or ".git" or "node_modules";

    private bool IsTestDir(string name) =>
        name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase);

    private bool IsTestFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase);
    }

    private bool HasSuffix(string path, IReadOnlyList<string> suffixes)
    {
        foreach (var s in suffixes)
            if (path.EndsWith(s, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private bool IsExcluded(string path, IReadOnlyList<string> exclude)
    {
        foreach (var e in exclude)
            if (!string.IsNullOrEmpty(e) && path.Contains(e, StringComparison.Ordinal)) return true;
        return false;
    }
}
