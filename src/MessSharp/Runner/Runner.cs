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

    public Report.Report Run(RunOptions opts)
    {
        var suffixes = opts.Suffixes.Count > 0 ? opts.Suffixes : new[] { ".cs" };
        var files = _discoverer.Discover(opts.Paths, suffixes, opts.Exclude, opts.IgnoreTests);
        var report = new Report.Report();

        foreach (var path in files)
        {
            SourceFile sf;
            try
            {
                sf = _parser.ParseFile(path);
            }
            catch (Exception ex)
            {
                report.Errors.Add(new ProcessingError { File = path, Message = ex.Message });
                continue;
            }

            if (TryRecordSyntaxErrors(sf, report)) continue;

            var violations = Engine.Analyze(sf, opts.RuleSets, opts.Strict);
            report.Violations.AddRange(violations);
        }

        RuleContext.SortViolations(report.Violations);
        return report;
    }

    /// <summary>
    /// Surfaces the parser's recoverable syntax diagnostics as processing errors.
    /// Returns true when the file is syntactically invalid and must not be analyzed:
    /// its partial tree would only yield spurious violations.
    /// </summary>
    private static bool TryRecordSyntaxErrors(SourceFile sf, Report.Report report)
    {
        var messages = sf.SyntaxErrorMessages;
        foreach (var message in messages)
            report.Errors.Add(new ProcessingError { File = sf.Path, Message = message });
        return messages.Count > 0;
    }
}
