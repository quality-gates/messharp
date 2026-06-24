namespace MessSharp.Report;

/// <summary>
/// Emits a simple HTML table report.
/// Mirrors messgo's HTMLRenderer.
/// </summary>
public sealed class HtmlRenderer : IRenderer
{
    public void Render(TextWriter w, Report report)
    {
        w.WriteLine("<!DOCTYPE html>");
        w.WriteLine("<html><head><meta charset=\"utf-8\"><title>messharp report</title></head><body>");
        w.WriteLine("<h1>messharp report</h1>");

        string curFile = "";
        bool open = false;

        foreach (var v in report.Violations)
        {
            if (v.File != curFile)
            {
                if (open) w.WriteLine("</table>");
                curFile = v.File;
                w.WriteLine($"<h2>{HtmlEscape(curFile)}</h2>");
                w.WriteLine("<table border=\"1\" cellspacing=\"0\" cellpadding=\"3\">");
                w.WriteLine("<tr><th>Line</th><th>Rule</th><th>Description</th></tr>");
                open = true;
            }
            w.WriteLine($"<tr><td>{v.BeginLine}</td><td>{HtmlEscape(v.Rule.Name)}</td><td>{HtmlEscape(v.Description)}</td></tr>");
        }

        if (open) w.WriteLine("</table>");
        w.WriteLine("</body></html>");
    }

    private static string HtmlEscape(string s) => XmlRenderer.XmlEscape(s);
}
