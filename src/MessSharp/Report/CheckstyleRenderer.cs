namespace MessSharp.Report;

/// <summary>
/// Emits Checkstyle XML.
/// Mirrors messgo's CheckStyleRenderer.
/// </summary>
public sealed class CheckstyleRenderer : IRenderer
{
    private const string ToolVersion = "0.1.0";

    public void Render(TextWriter w, Report report)
    {
        w.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        w.WriteLine($"<checkstyle version=\"{ToolVersion}\">");

        string curFile = "";
        bool open = false;

        foreach (var v in report.Violations)
        {
            if (v.File != curFile)
            {
                if (open) w.WriteLine("  </file>");
                curFile = v.File;
                w.WriteLine($"  <file name=\"{XmlRenderer.XmlEscape(curFile)}\">");
                open = true;
            }
            w.WriteLine($"    <error line=\"{v.BeginLine}\" column=\"1\" severity=\"{Severity(v.Priority)}\" message=\"{XmlRenderer.XmlEscape(v.Description)}\" source=\"{XmlRenderer.XmlEscape(v.RuleSetName + "/" + v.Rule.Name)}\"/>");
        }

        if (open) w.WriteLine("  </file>");
        w.WriteLine("</checkstyle>");
    }

    private static string Severity(int priority) =>
        priority <= 2 ? "error" : priority == 3 ? "warning" : "info";
}
