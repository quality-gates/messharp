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

    public void Render(TextWriter w, Report report)
    {
        w.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" ?>");
        w.WriteLine($"<pmd version=\"{BuildInfo.Version}\" tool=\"{ToolName}\" timestamp=\"{DateTime.Now:o}\">");

        foreach (var file in report.GetFiles())
        {
            if (file.Violations.Count == 0) continue;
            w.WriteLine($"  <file name=\"{XmlEscape(file.Path)}\">");
            foreach (var v in file.Violations)
            {
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
            w.WriteLine("  </file>");
        }

        foreach (var e in report.Errors)
            w.WriteLine($"  <error filename=\"{XmlEscape(e.File)}\" msg=\"{XmlEscape(e.Message)}\" />");

        w.WriteLine("</pmd>");
    }

    private static void MaybeAttr(StringBuilder sb, string name, string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return;
        sb.Append($" {name}=\"{XmlEscape(val)}\"");
    }

    internal static string XmlEscape(string s) => Escape.Xml(s);
}
