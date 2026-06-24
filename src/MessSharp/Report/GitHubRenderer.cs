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
            w.WriteLine($"::warning file={v.File},line={v.BeginLine},col=1::{v.Description} ({v.Rule.Name})");

        foreach (var e in report.Errors)
            w.WriteLine($"::error file={e.File}::{e.Message}");
    }
}
