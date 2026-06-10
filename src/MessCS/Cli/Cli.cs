using MessCS.Report;
using MessCS.Rule;
using MessCS.Runner;
using RuleSetLoader = MessCS.RuleSet.Loader;
using RuleSetType = MessCS.Rule.RuleSet;

namespace MessCS.Cli;

public static class Cli
{
    private const int ExitSuccess   = 0;
    private const int ExitError     = 1;
    private const int ExitViolation = 2;

    private const string Version = "0.1.0";

    public static int Run(string[] args, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        if (args.Length == 0) { PrintUsage(stderr); return ExitError; }

        if (HandleInfoFlag(args[0], stdout))
            return ExitSuccess;

        var (opts, positionals, err) = ParseArgs(args);
        if (err != null)
        {
            stderr.WriteLine($"error: {err}");
            return ExitError;
        }
        if (positionals.Count < 3)
        {
            PrintUsage(stderr);
            return ExitError;
        }

        opts.Paths    = positionals[0];
        opts.Format   = positionals[1];
        opts.Rulesets = positionals[2];

        return Execute(opts, stdout, stderr);
    }

    private static bool HandleInfoFlag(string first, TextWriter stdout)
    {
        if (first == "--version") { stdout.WriteLine($"messcs {Version}"); return true; }
        if (first is "--help" or "-h" or "help") { PrintUsage(stdout); return true; }
        return false;
    }

    private static int Execute(CliOptions opts, TextWriter stdout, TextWriter stderr)
    {
        if (!Renderers.TryGet(opts.Format, out var renderer))
        {
            stderr.WriteLine($"error: unknown report format \"{opts.Format}\". Available: {string.Join(", ", Renderers.Formats)}");
            return ExitError;
        }

        List<RuleSetType> sets;
        try { sets = LoadRuleSets(opts, stderr); }
        catch (Exception ex) { stderr.WriteLine($"error: {ex.Message}"); return ExitError; }

        RuleSetLoader.FilterRules(sets, SplitList(opts.Only ?? opts.Enable ?? ""), SplitList(opts.Disable));

        MessCS.Report.Report report;
        try
        {
            report = MessCS.Runner.Runner.Run(new RunOptions
            {
                Paths       = SplitList(opts.Paths),
                RuleSets    = sets,
                Suffixes    = SuffixList(opts.Suffixes),
                Exclude     = SplitList(opts.Exclude),
                IgnoreTests = opts.IgnoreTests,
            });
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return ExitError;
        }

        try
        {
            TextWriter dest = stdout;
            if (!string.IsNullOrEmpty(opts.ReportFile))
            {
                dest = new StreamWriter(opts.ReportFile, append: false);
            }
            using (dest != stdout ? dest : (IDisposable)new NoopDisposable())
                renderer.Render(dest, report);
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return ExitError;
        }

        if (report.Errors.Count > 0 && !opts.IgnoreErrors)   return ExitError;
        if (report.Violations.Count > 0 && !opts.IgnoreViolations) return ExitViolation;
        return ExitSuccess;
    }

    private static List<RuleSetType> LoadRuleSets(CliOptions opts, TextWriter stderr)
    {
        var loader = new RuleSetLoader
        {
            MinPriority = opts.MinPriority,
            MaxPriority = opts.MaxPriority,
            Warn = msg => { if (opts.Verbose) stderr.WriteLine($"warning: {msg}"); },
        };
        return loader.Load(opts.Rulesets);
    }

    private static (CliOptions opts, List<string> positionals, string? error) ParseArgs(string[] args)
    {
        var opts = new CliOptions { MaxPriority = 1 };
        var positionals = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--verbose": case "-v": opts.Verbose = true; break;
                case "--strict":             opts.Strict = true; break;
                case "--color":              opts.Color = true; break;
                case "--ignore-errors-on-exit":     opts.IgnoreErrors = true; break;
                case "--ignore-violations-on-exit": opts.IgnoreViolations = true; break;
                case "--ignore-tests":       opts.IgnoreTests = true; break;
                case "--reportfile":
                    i++; opts.ReportFile = Arg(args, i); break;
                case "--suffixes":
                    i++; opts.Suffixes = Arg(args, i); break;
                case "--exclude":
                    i++; opts.Exclude = Arg(args, i); break;
                case "--enable": case "--only":
                    i++; opts.Only = Arg(args, i); break;
                case "--disable":
                    i++; opts.Disable = Arg(args, i); break;
                case "--minimumpriority":
                    i++;
                    if (!int.TryParse(Arg(args, i), out var min))
                        return (opts, positionals, $"--minimumpriority requires an integer");
                    opts.MinPriority = min; break;
                case "--maximumpriority":
                    i++;
                    if (!int.TryParse(Arg(args, i), out var max))
                        return (opts, positionals, $"--maximumpriority requires an integer");
                    opts.MaxPriority = max; break;
                default:
                    if (a.StartsWith("--"))
                        return (opts, positionals, $"unknown option: {a}");
                    positionals.Add(a);
                    break;
            }
        }
        return (opts, positionals, null);
    }

    private static string Arg(string[] args, int i) =>
        i < args.Length ? args[i] : "";

    private static List<string> SplitList(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new();
        return s.Split(',')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();
    }

    private static List<string> SuffixList(string? s)
    {
        var parts = SplitList(s);
        if (parts.Count == 0) return new List<string> { ".cs" };
        return parts.Select(p => p.StartsWith('.') ? p : "." + p).ToList();
    }

    private static void PrintUsage(TextWriter w)
    {
        w.WriteLine($"messcs {Version} — a phpmd-style mess detector for C#");
        w.WriteLine();
        w.WriteLine("Usage:");
        w.WriteLine("  messcs <paths> <format> <ruleset[,...]> [options]");
        w.WriteLine();
        w.WriteLine("Arguments:");
        w.WriteLine("  paths      Comma-separated files or directories to scan.");
        w.WriteLine($"  format     Report format: {string.Join(", ", Renderers.Formats)}");
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

internal sealed class CliOptions
{
    public string Paths    { get; set; } = "";
    public string Format   { get; set; } = "text";
    public string Rulesets { get; set; } = "";
    public int    MinPriority { get; set; }
    public int    MaxPriority { get; set; } = 1;
    public string ReportFile { get; set; } = "";
    public string Suffixes   { get; set; } = "";
    public string Exclude    { get; set; } = "";
    public string? Enable    { get; set; }
    public string? Only      { get; set; }
    public string Disable    { get; set; } = "";
    public bool Strict           { get; set; }
    public bool Color            { get; set; }
    public bool Verbose          { get; set; }
    public bool IgnoreErrors     { get; set; }
    public bool IgnoreViolations { get; set; }
    public bool IgnoreTests      { get; set; }
}

internal sealed class NoopDisposable : IDisposable
{
    public void Dispose() { }
}
