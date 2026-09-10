using RuleSetLoader = MessSharp.RuleSet.Loader;

namespace MessSharp.Cli;

/// <summary>
/// Parses CLI arguments into CliOptions. Extracted from Cli to reduce Cli's
/// weighted method count.
/// </summary>
internal static class CliArgParser
{
    // Boolean flags: flag name -> setter
    private static readonly Dictionary<string, Action<CliOptions>> BoolFlags =
        new(StringComparer.Ordinal)
        {
            ["--verbose"] = o => o.Verbose = true,
            ["-v"] = o => o.Verbose = true,
            ["--strict"] = o => o.Strict = true,
            ["--color"] = o => o.Color = true,
            ["--ignore-errors-on-exit"] = o => o.IgnoreErrors = true,
            ["--ignore-violations-on-exit"] = o => o.IgnoreViolations = true,
            ["--ignore-tests"] = o => o.IgnoreTests = true,
        };

    // String-valued options: flag name -> setter
    private static readonly Dictionary<string, Action<CliOptions, string>> StringFlags =
        new(StringComparer.Ordinal)
        {
            ["--reportfile"] = (o, v) => o.ReportFile = v,
            ["--suffixes"] = (o, v) => o.Suffixes = v,
            ["--exclude"] = (o, v) => o.Filters.Exclude = v,
            ["--enable"] = (o, v) => o.Filters.Only = v,
            ["--only"] = (o, v) => o.Filters.Only = v,
            ["--disable"] = (o, v) => o.Filters.Disable = v,
        };

    // Integer-valued options: flag name -> setter
    private static readonly Dictionary<string, Action<CliOptions, int>> IntFlags =
        new(StringComparer.Ordinal)
        {
            ["--minimumpriority"] = (o, v) => o.MinPriority = v,
            ["--maximumpriority"] = (o, v) => o.MaxPriority = v,
        };

    internal static (CliOptions opts, List<string> positionals, string? error) Parse(string[] args)
    {
        var opts = new CliOptions { MaxPriority = 1 };
        var positionals = new List<string>();

        int len = args.Length;
        for (int i = 0; i < len; i++)
        {
            var err = ParseOne(args, ref i, opts, positionals);
            if (err != null) return (opts, positionals, err);
        }
        return (opts, positionals, null);
    }

    private static string? ParseOne(string[] args, ref int i, CliOptions opts, List<string> positionals)
    {
        var a = args[i];

        if (BoolFlags.TryGetValue(a, out var setter)) { setter(opts); return null; }
        if (StringFlags.TryGetValue(a, out var setString)) return ParseStringFlag(a, args, ref i, opts, setString);
        if (IntFlags.TryGetValue(a, out var setInt)) return ParseIntFlag(a, args, ref i, opts, setInt);
        if (a.StartsWith("--")) return $"unknown option: {a}";

        positionals.Add(a);
        return null;
    }

    private static string? ParseStringFlag(string flag, string[] args, ref int i,
        CliOptions opts, Action<CliOptions, string> set)
    {
        if (!TryTakeValue(args, ref i, out var value)) return MissingValue(flag);
        set(opts, value);
        return null;
    }

    private static string? ParseIntFlag(string flag, string[] args, ref int i,
        CliOptions opts, Action<CliOptions, int> set)
    {
        if (!TryTakeValue(args, ref i, out var value)) return MissingValue(flag);
        if (!int.TryParse(value, out var parsed)) return $"{flag} requires an integer";
        set(opts, parsed);
        return null;
    }

    private static string MissingValue(string flag) => $"{flag} requires a value";

    /// <summary>
    /// Consumes the argument after the current option as its value. A known flag
    /// (or end of input) is never consumed as a value, so a missing value is reported
    /// rather than silently swallowing the next option.
    /// </summary>
    private static bool TryTakeValue(string[] args, ref int i, out string value)
    {
        int next = i + 1;
        if (next >= args.Length || IsKnownFlag(args[next]))
        {
            value = "";
            return false;
        }
        i = next;
        value = args[next];
        return true;
    }

    private static bool IsKnownFlag(string a) =>
        BoolFlags.ContainsKey(a) || StringFlags.ContainsKey(a) || IntFlags.ContainsKey(a);

    internal static List<string> SplitList(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new();
        return s.Split(',')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();
    }

    internal static List<string> SuffixList(string? s)
    {
        var parts = SplitList(s);
        if (parts.Count == 0) return new List<string> { ".cs" };
        return parts.Select(p => p.StartsWith('.') ? p : "." + p).ToList();
    }

    internal static void PrintUsage(TextWriter w, string version)
    {
        w.WriteLine($"messharp {version} — a phpmd-style mess detector for C#");
        w.WriteLine();
        w.WriteLine("Usage:");
        w.WriteLine("  messharp <paths> <format> <ruleset[,...]> [options]");
        w.WriteLine();
        w.WriteLine("Arguments:");
        w.WriteLine("  paths      Comma-separated files or directories to scan.");
        w.WriteLine($"  format     Report format: {string.Join(", ", MessSharp.Report.Renderers.Formats)}");
        w.WriteLine($"  ruleset    Comma-separated built-in rulesets or ruleset XML files.");
        w.WriteLine($"             Built-in: {string.Join(", ", RuleSetLoader.BuiltinRulesetNames)}");
        w.WriteLine();
        w.WriteLine("Options:");
        w.WriteLine("  --minimumpriority <n>          Only rules with priority <= n.");
        w.WriteLine("  --maximumpriority <n>          Only rules with priority >= n.");
        w.WriteLine("  --reportfile <file>            Write the report to a file.");
        w.WriteLine("  --suffixes <list>              File extensions to scan (default: cs).");
        w.WriteLine("  --exclude <list>               Path substrings to exclude.");
        w.WriteLine("  --enable, --only <list>        Run only these rules.");
        w.WriteLine("  --disable <list>               Skip these rules.");
        w.WriteLine("  --ignore-tests                 Skip *Test.cs/*Tests.cs files.");
        w.WriteLine("  --strict                       Also report suppressed violations.");
        w.WriteLine("  --color                        Colorize text output.");
        w.WriteLine("  --verbose, -v                  Verbose diagnostics.");
        w.WriteLine("  --ignore-errors-on-exit        Exit 0 even if parse errors occurred.");
        w.WriteLine("  --ignore-violations-on-exit    Exit 0 even if violations were found.");
        w.WriteLine("  --version                      Print version.");
        w.WriteLine("  --help, -h                     Show this help.");
        w.WriteLine();
        w.WriteLine("Exit codes: 0 = clean, 1 = error, 2 = violations found.");
    }
}
