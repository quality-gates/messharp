using System.Text;
using MessSharp.Rule;

namespace MessSharp.Report;

/// <summary>
/// Reproduces PHPMD's PMD-compatible XML output.
/// Mirrors messgo's XMLRenderer.
/// </summary>
public sealed class XmlRenderer : IRenderer
{
    private const string ToolName = "messharp";
    private const string ToolVersion = "0.1.0";

    public void Render(TextWriter w, Report report)
    {
        w.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" ?>");
        w.WriteLine($"<pmd version=\"{ToolVersion}\" tool=\"{ToolName}\" timestamp=\"{DateTime.Now:o}\">");

        string curFile = "";
        bool open = false;

        foreach (var v in report.Violations)
        {
            if (v.File != curFile)
            {
                if (open) w.WriteLine("  </file>");
                curFile = v.File;
                w.WriteLine($"  <file name=\"{XmlEscape(curFile)}\">");
                open = true;
            }

            var sb = new StringBuilder("    <violation");
            sb.Append($" beginline=\"{v.BeginLine}\"");
            sb.Append($" endline=\"{v.EndLine}\"");
            sb.Append($" rule=\"{XmlEscape(v.Rule.Name)}\"");
            sb.Append($" ruleset=\"{XmlEscape(v.RuleSetName)}\"");
            MaybeAttr(sb, "package", v.Package);
            MaybeAttr(sb, "externalInfoUrl", v.Rule.ExternalUrl);
            MaybeAttr(sb, "function", v.Function);
            MaybeAttr(sb, "class", v.Class);
            MaybeAttr(sb, "method", v.Method);
            sb.Append($" priority=\"{v.Priority}\"");
            sb.Append('>');
            w.WriteLine(sb.ToString());
            w.WriteLine($"      {XmlEscape(v.Description)}");
            w.WriteLine("    </violation>");
        }

        if (open) w.WriteLine("  </file>");

        foreach (var e in report.Errors)
            w.WriteLine($"  <error filename=\"{XmlEscape(e.File)}\" msg=\"{XmlEscape(e.Message)}\" />");

        w.WriteLine("</pmd>");
    }

    private static void MaybeAttr(StringBuilder sb, string name, string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return;
        sb.Append($" {name}=\"{XmlEscape(val)}\"");
    }

    internal static string XmlEscape(string s) =>
        s.Replace("&", "&amp;")
         .Replace("<", "&lt;")
         .Replace(">", "&gt;")
         .Replace("\"", "&quot;")
         .Replace("'", "&#039;");
}
