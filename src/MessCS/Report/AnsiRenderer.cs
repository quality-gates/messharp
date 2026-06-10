namespace MessCS.Report;

/// <summary>
/// Column-aligned text with ANSI colours: rule name in yellow (33), description in red (31).
/// Mirrors messgo's TextRenderer{Colored: true}.
/// </summary>
public sealed class AnsiRenderer : IRenderer
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
            w.Write(Colorize(name, "33"));
            w.Write(new string(' ', longestRule + Spacing - name.Length));
            w.Write(Colorize(desc, "31"));
            w.WriteLine();
        }

        foreach (var e in report.Errors)
            w.WriteLine($"{e.File}\t-\t{e.Message}");
    }

    private static string Colorize(string s, string code) =>
        $"\x1b[{code}m{s}\x1b[0m";
}
