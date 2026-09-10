namespace MessSharp.Report;

/// <summary>
/// Emits GitHub Actions workflow commands.
/// Mirrors messgo's GitHubRenderer.
/// </summary>
public sealed class GitHubRenderer : IRenderer
{
    public void Render(TextWriter w, Report report)
    {
        foreach (var v in report.Violations)
            w.WriteLine($"::warning file={EscapeWorkflowCommandProperty(v.File)},line={v.BeginLine},col=1::{v.Description} ({v.Rule.Name})");

        foreach (var e in report.Errors)
            w.WriteLine($"::error file={EscapeWorkflowCommandProperty(e.File)}::{e.Message}");
    }

    private static string EscapeWorkflowCommandProperty(string value) =>
        value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal)
            .Replace(":", "%3A", StringComparison.Ordinal)
            .Replace(",", "%2C", StringComparison.Ordinal);
}
