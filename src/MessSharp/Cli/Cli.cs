using MessSharp.Report;
using MessSharp.Rule;
using MessSharp.Runner;
using RuleSetLoader = MessSharp.RuleSet.Loader;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.Cli;

public static class Cli
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitViolation = 2;

    private const string Version = "0.2.2";

    public static int Run(string[] args, TextWriter? stdout = null, TextWriter? stderr = null, IRunner? runner = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        if (args.Length == 0) { CliArgParser.PrintUsage(stderr, Version); return ExitError; }
        if (HandleInfoFlag(args[0], stdout)) return ExitSuccess;

        var (opts, positionals, err) = CliArgParser.Parse(args);
        if (err != null) { stderr.WriteLine($"error: {err}"); return ExitError; }
        if (positionals.Count < 3) { CliArgParser.PrintUsage(stderr, Version); return ExitError; }

        opts.Paths = positionals[0];
        opts.Format = positionals[1];
        opts.Rulesets = positionals[2];

        return Execute(opts, stdout, stderr, runner);
    }

    private static bool HandleInfoFlag(string first, TextWriter stdout)
    {
        if (first == "--version") { stdout.WriteLine($"messharp {Version}"); return true; }
        if (first is "--help" or "-h" or "help") { CliArgParser.PrintUsage(stdout, Version); return true; }
        return false;
    }

    private static int Execute(CliOptions opts, TextWriter stdout, TextWriter stderr, IRunner? runner = null)
    {
        if (!Renderers.TryGet(opts.Format, out var renderer))
        {
            stderr.WriteLine($"error: unknown report format \"{opts.Format}\". Available: {string.Join(", ", Renderers.Formats)}");
            return ExitError;
        }

        List<RuleSetType> sets;
        try { sets = LoadAndFilterRuleSets(opts, stderr); }
        catch (Exception ex) { stderr.WriteLine($"error: {ex.Message}"); return ExitError; }

        var report = RunAnalysis(opts, sets, stderr, runner);
        if (report == null) return ExitError;

        if (!WriteReport(opts, report, renderer, stdout, stderr)) return ExitError;

        if (report.Errors.Count > 0 && !opts.IgnoreErrors) return ExitError;
        if (report.Violations.Count > 0 && !opts.IgnoreViolations) return ExitViolation;
        return ExitSuccess;
    }

    private static List<RuleSetType> LoadAndFilterRuleSets(CliOptions opts, TextWriter stderr)
    {
        var sets = LoadRuleSets(opts, stderr);
        RuleSetLoader.FilterRules(sets,
            CliArgParser.SplitList(opts.Filters.Only ?? opts.Filters.Enable ?? ""),
            CliArgParser.SplitList(opts.Filters.Disable));
        return sets;
    }

    private static MessSharp.Report.Report? RunAnalysis(CliOptions opts, List<RuleSetType> sets, TextWriter stderr, IRunner? runner = null)
    {
        try
        {
            runner ??= new MessSharp.Runner.Runner();
            return runner.Run(new RunOptions
            {
                Paths = CliArgParser.SplitList(opts.Paths),
                RuleSets = sets,
                Suffixes = CliArgParser.SuffixList(opts.Suffixes),
                Exclude = CliArgParser.SplitList(opts.Filters.Exclude),
                IgnoreTests = opts.IgnoreTests,
            });
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return null;
        }
    }

    private static bool WriteReport(CliOptions opts, MessSharp.Report.Report report,
        IRenderer renderer, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            TextWriter dest = stdout;
            if (!string.IsNullOrEmpty(opts.ReportFile))
                dest = new StreamWriter(opts.ReportFile, append: false);
            using (dest != stdout ? dest : (IDisposable)new NoopDisposable())
                renderer.Render(dest, report);
            return true;
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"error: {ex.Message}");
            return false;
        }
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
}

/// <summary>Filter-related CLI options grouped to reduce field count on CliOptions.</summary>
internal sealed class FilterOptions
{
    public string? Enable { get; set; }
    public string? Only { get; set; }
    public string Disable { get; set; } = "";
    public string Exclude { get; set; } = "";
}

internal sealed class CliOptions
{
    public string Paths { get; set; } = "";
    public string Format { get; set; } = "text";
    public string Rulesets { get; set; } = "";
    public int MinPriority { get; set; }
    public int MaxPriority { get; set; } = 1;
    public string ReportFile { get; set; } = "";
    public string Suffixes { get; set; } = "";
    public FilterOptions Filters { get; set; } = new();
    public bool Strict { get; set; }
    public bool Color { get; set; }
    public bool Verbose { get; set; }
    public bool IgnoreErrors { get; set; }
    public bool IgnoreViolations { get; set; }
    public bool IgnoreTests { get; set; }
}

internal sealed class NoopDisposable : IDisposable
{
    public void Dispose() { }
}
