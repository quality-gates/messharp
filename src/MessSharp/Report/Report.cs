using MessSharp.Rule;

namespace MessSharp.Report;

public sealed class ProcessingError
{
    public string File { get; init; } = "";
    public string Message { get; init; } = "";
    /// <summary>1-based line the error points at; null when no line is known (e.g. I/O failures).</summary>
    public int? Line { get; init; }
}

public sealed class Report
{
    public List<Violation> Violations { get; init; } = new();
    public List<ProcessingError> Errors { get; init; } = new();
}

public interface IRenderer
{
    void Render(TextWriter w, Report report);
}
