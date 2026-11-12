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

        foreach (var file in report.GetFiles())
        {
            if (file.Violations.Count == 0) continue;
            w.WriteLine($"<h2>{HtmlEscape(file.Path)}</h2>");
            w.WriteLine("<table border=\"1\" cellspacing=\"0\" cellpadding=\"3\">");
            w.WriteLine("<tr><th>Line</th><th>Rule</th><th>Description</th></tr>");
            foreach (var v in file.Violations)
                w.WriteLine($"<tr><td>{v.BeginLine}</td><td>{HtmlEscape(v.Rule.Name)}</td><td>{HtmlEscape(v.Description)}</td></tr>");
            w.WriteLine("</table>");
        }

        foreach (var e in report.Errors)
            w.WriteLine($"<p>{HtmlEscape(e.File)}: {HtmlEscape(e.Message)}</p>");

        w.WriteLine("</body></html>");
    }

    private static string HtmlEscape(string s) => Escape.Xml(s);
}
