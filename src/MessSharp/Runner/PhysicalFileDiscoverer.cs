namespace MessSharp.Runner;

public sealed class PhysicalFileDiscoverer : IFileDiscoverer
{
    private readonly StringComparer _pathComparer;

    public PhysicalFileDiscoverer()
        : this(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
    {
    }

    public PhysicalFileDiscoverer(StringComparer pathComparer)
    {
        _pathComparer = pathComparer;
    }

    public List<string> Discover(
        IReadOnlyList<string> paths,
        IReadOnlyList<string> suffixes,
        IReadOnlyList<string> exclude,
        bool ignoreTests)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(_pathComparer);

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
                AddFileEntry(p, suffixes, exclude, ignoreTests, Add);
            else
                // phpmd/messgo error out on a path that does not exist
                throw new FileNotFoundException($"no such file or directory: {p}");
        }

        result.Sort(ComparePaths);
        return result;
    }

    private static int ComparePaths(string a, string b)
    {
        int c = StringComparer.OrdinalIgnoreCase.Compare(a, b);
        return c != 0 ? c : StringComparer.Ordinal.Compare(a, b);
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
        if (IsDirectorySymlink(entry)) return;
        if (ignoreTests && IsTestDir(name)) return;
        WalkDir(entry, suffixes, exclude, ignoreTests, add);
    }

    private static bool IsDirectorySymlink(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
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
