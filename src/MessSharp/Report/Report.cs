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

    public IReadOnlyList<ReportFile> GetFiles()
    {
        var files = new SortedDictionary<string, ReportFile>(StringComparer.Ordinal);
        foreach (var violation in Violations)
        {
            if (!files.TryGetValue(violation.File, out var file))
                files.Add(violation.File, file = new ReportFile(violation.File));
            file.Violations.Add(violation);
        }
        foreach (var error in Errors)
        {
            var path = error.File ?? "";
            if (!files.TryGetValue(path, out var file))
                files.Add(path, file = new ReportFile(path));
            file.Errors.Add(error);
        }
        foreach (var file in files.Values)
        {
            var ordered = file.Violations.OrderBy(v => v.BeginLine).ToList();
            file.Violations.Clear();
            file.Violations.AddRange(ordered);
        }
        return files.Values.ToList();
    }

    public void SortViolations() => SortViolations(Violations);

    internal static void SortViolations(List<Violation> violations) =>
        violations.Sort((a, b) =>
        {
            int comparison = string.Compare(a.File, b.File, StringComparison.Ordinal);
            return comparison != 0 ? comparison : a.BeginLine.CompareTo(b.BeginLine);
        });
}

public sealed class ReportFile
{
    internal ReportFile(string path) => Path = path;
    public string Path { get; }
    public List<Violation> Violations { get; } = new();
    public List<ProcessingError> Errors { get; } = new();
}

public interface IRenderer
{
    void Render(TextWriter w, Report report);
}
