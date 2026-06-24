namespace MessSharp.Report;

/// <summary>
/// Registry of all report format renderers.
/// Only the `text` renderer is implemented; all others throw a clear error.
/// </summary>
public static class Renderers
{
    private static readonly Dictionary<string, Func<IRenderer>> _map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["text"] = () => new TextRenderer(),
            ["xml"] = () => new XmlRenderer(),
            ["json"] = () => new JsonRenderer(),
            ["html"] = () => new HtmlRenderer(),
            ["ansi"] = () => new AnsiRenderer(),
            ["github"] = () => new GitHubRenderer(),
            ["gitlab"] = () => new GitLabRenderer(),
            ["checkstyle"] = () => new CheckstyleRenderer(),
            ["sarif"] = () => new SarifRenderer(),
        };

    public static bool TryGet(string format, out IRenderer renderer)
    {
        if (_map.TryGetValue(format, out var factory))
        {
            renderer = factory();
            return true;
        }
        renderer = null!;
        return false;
    }

    public static IReadOnlyList<string> Formats =>
        new[] { "text", "xml", "json", "html", "ansi", "github", "gitlab", "checkstyle", "sarif" };
}

/// <summary>
/// Renders violations as plain text: file:line  Rule  message.
/// Mirrors messgo's TextRenderer (column-aligned).
/// </summary>
public sealed class TextRenderer : IRenderer
{
    private const int Spacing = 2;

    public void Render(TextWriter w, Report report)
    {
        int longestLoc = 0, longestRule = 0;
        var rows = new List<(string loc, string name, string desc)>();

        foreach (var v in report.Violations)
        {
            var loc = $"{v.File}:{v.BeginLine}";
            var name = v.Rule.Name;
            if (loc.Length > longestLoc) longestLoc = loc.Length;
            if (name.Length > longestRule) longestRule = name.Length;
            rows.Add((loc, name, v.Description));
        }

        foreach (var (loc, name, desc) in rows)
        {
            w.Write(loc);
            w.Write(new string(' ', longestLoc + Spacing - loc.Length));
            w.Write(name);
            w.Write(new string(' ', longestRule + Spacing - name.Length));
            w.Write(desc);
            w.WriteLine();
        }

        foreach (var e in report.Errors)
            w.WriteLine($"{e.File}\t-\t{e.Message}");
    }
}

internal sealed class NotImplementedRenderer : IRenderer
{
    private readonly string _format;
    public NotImplementedRenderer(string format) => _format = format;

    public void Render(TextWriter w, Report report) =>
        throw new NotSupportedException(
            $"Report format '{_format}' is not implemented yet. " +
            $"Available: {string.Join(", ", Renderers.Formats)}");
}
