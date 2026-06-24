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
            ["--verbose"]                   = o => o.Verbose = true,
            ["-v"]                          = o => o.Verbose = true,
            ["--strict"]                    = o => o.Strict = true,
            ["--color"]                     = o => o.Color = true,
            ["--ignore-errors-on-exit"]     = o => o.IgnoreErrors = true,
            ["--ignore-violations-on-exit"] = o => o.IgnoreViolations = true,
            ["--ignore-tests"]              = o => o.IgnoreTests = true,
        };

    internal static (CliOptions opts, List<string> positionals, string? error) Parse(string[] args)
    {
        var opts = new CliOptions { MaxPriority = 1 };
        var positionals = new List<string>();

        for (int i = 0; i < args.Length; i++)
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
        if (TryParseValueFlag(a, args, ref i, opts, out var err)) return err;
        if (a.StartsWith("--")) return $"unknown option: {a}";

        positionals.Add(a);
        return null;
    }

    private static bool TryParseValueFlag(string a, string[] args, ref int i, CliOptions opts, out string? err)
    {
        err = null;
        if (TryParseStringFlag(a, args, ref i, opts)) return true;
        return TryParseIntFlag(a, args, ref i, opts, out err);
    }

    private static bool TryParseStringFlag(string a, string[] args, ref int i, CliOptions opts)
    {
        switch (a)
        {
            case "--reportfile":  i++; opts.ReportFile = NextArg(args, i); return true;
            case "--suffixes":    i++; opts.Suffixes = NextArg(args, i); return true;
            case "--exclude":     i++; opts.Filters.Exclude = NextArg(args, i); return true;
            case "--enable":
            case "--only":        i++; opts.Filters.Only = NextArg(args, i); return true;
            case "--disable":     i++; opts.Filters.Disable = NextArg(args, i); return true;
            default:              return false;
        }
    }

    private static bool TryParseIntFlag(string a, string[] args, ref int i, CliOptions opts, out string? err)
    {
        err = null;
        switch (a)
        {
            case "--minimumpriority":
                if (!TryParseInt(a, args, ref i, out var min, out err)) return true;
                opts.MinPriority = min; return true;
            case "--maximumpriority":
                if (!TryParseInt(a, args, ref i, out var max, out err)) return true;
                opts.MaxPriority = max; return true;
            default:
                return false;
        }
    }

    private static bool TryParseInt(string flag, string[] args, ref int i, out int value, out string? err)
    {
        i++;
        if (int.TryParse(NextArg(args, i), out value)) { err = null; return true; }
        err = $"{flag} requires an integer";
        return false;
    }

    internal static string NextArg(string[] args, int i) =>
        i < args.Length ? args[i] : "";

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
