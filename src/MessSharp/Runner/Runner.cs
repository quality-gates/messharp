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

            var violations = Engine.Analyze(sf, opts.RuleSets);
            report.Violations.AddRange(violations);
        }

        RuleContext.SortViolations(report.Violations);
        return report;
    }
}
