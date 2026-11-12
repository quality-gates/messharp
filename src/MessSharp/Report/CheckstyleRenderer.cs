namespace MessSharp.Report;

/// <summary>
/// Emits Checkstyle XML.
/// Mirrors messgo's CheckStyleRenderer.
/// </summary>
public sealed class CheckstyleRenderer : IRenderer
{
    public void Render(TextWriter w, Report report)
    {
        w.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        w.WriteLine($"<checkstyle version=\"{BuildInfo.Version}\">");

        foreach (var file in report.GetFiles())
        {
            w.WriteLine($"  <file name=\"{XmlRenderer.XmlEscape(file.Path)}\">");
            foreach (var v in file.Violations)
                w.WriteLine($"    <error line=\"{v.BeginLine}\" column=\"1\" severity=\"{Severity(v.Priority)}\" message=\"{XmlRenderer.XmlEscape(v.Description)}\" source=\"{XmlRenderer.XmlEscape(v.RuleSetName + "/" + v.Rule.Name)}\"/>");
            foreach (var e in file.Errors)
                w.WriteLine($"    <error line=\"0\" column=\"1\" severity=\"error\" message=\"{XmlRenderer.XmlEscape(e.Message)}\" source=\"messharp/parse-error\"/>");
            w.WriteLine("  </file>");
        }

        w.WriteLine("</checkstyle>");
    }

    private static string Severity(int priority) =>
        priority <= 2 ? "error" : priority == 3 ? "warning" : "info";
}
