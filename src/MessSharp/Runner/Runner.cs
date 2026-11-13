using MessSharp.Model;
using MessSharp.Report;
using MessSharp.Rule;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.Runner;

public sealed class RunOptions
{
    public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RuleSetType> RuleSets { get; init; } = Array.Empty<RuleSetType>();
    public IReadOnlyList<string> Suffixes { get; init; } = new[] { ".cs" };
    public IReadOnlyList<string> Exclude { get; init; } = Array.Empty<string>();
    public bool IgnoreTests { get; init; }
    public bool Strict { get; init; }
}

public sealed class Runner : IRunner
{
    private readonly IFileDiscoverer _discoverer;
    private readonly ISourceFileParser _parser;

    public Runner() : this(new PhysicalFileDiscoverer(), new RoslynSourceFileParser())
    {
    }

    public Runner(IFileDiscoverer discoverer, ISourceFileParser parser)
    {
        _discoverer = discoverer;
        _parser = parser;
    }

    /// <summary>What became of one file on its way in. Only <see cref="Analyzable"/> files are analyzed.</summary>
    private enum Intake { Analyzable, Unreadable, SyntaxInvalid }

    /// <summary>Discover, then intake every file, then analyze the analyzable ones together, then sort.</summary>
    public Report.Report Run(RunOptions opts)
    {
        var suffixes = opts.Suffixes.Count > 0 ? opts.Suffixes : new[] { ".cs" };
        var files = _discoverer.Discover(opts.Paths, suffixes, opts.Exclude, opts.IgnoreTests);
        var report = new Report.Report();

        // Parse everything before analyzing: partial types can span files.
        var analyzable = new List<SourceFile>();
        foreach (var path in files)
        {
            var (outcome, file) = Receive(path, report);
            if (outcome == Intake.Analyzable) analyzable.Add(file!);
        }

        report.Violations.AddRange(Engine.AnalyzeAll(analyzable, opts.RuleSets, opts.Strict));
        report.SortViolations();
        return report;
    }

    /// <summary>
    /// Parses one file and records what went wrong. A file that cannot be read, or whose syntax is invalid, is not
    /// analyzed: its partial tree would only yield spurious violations.
    /// </summary>
    private (Intake Outcome, SourceFile? File) Receive(string path, Report.Report report)
    {
        var file = TryParse(path, report);
        if (file is null) return (Intake.Unreadable, null);
        return TryRecordSyntaxErrors(file, report) ? (Intake.SyntaxInvalid, null) : (Intake.Analyzable, file);
    }

    private SourceFile? TryParse(string path, Report.Report report)
    {
        try
        {
            return _parser.ParseFile(path);
        }
        catch (Exception ex)
        {
            report.Errors.Add(new ProcessingError { File = path, Message = ex.Message });
            return null;
        }
    }

    /// <summary>
    /// Surfaces the parser's recoverable syntax diagnostics as processing errors.
    /// Returns true when the file is syntactically invalid and must not be analyzed:
    /// its partial tree would only yield spurious violations.
    /// </summary>
    private static bool TryRecordSyntaxErrors(SourceFile sf, Report.Report report)
    {
        var errors = sf.SyntaxErrors;
        foreach (var error in errors)
            report.Errors.Add(new ProcessingError { File = sf.Path, Message = error.Message, Line = error.Line });
        return errors.Count > 0;
    }
}
