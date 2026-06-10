using MessCS.Model;

namespace MessCS.Rule;

/// <summary>
/// Handed to each rule during analysis. Rules append violations via helpers.
/// Mirrors messgo's Context.
/// </summary>
public sealed class RuleContext
{
    private readonly List<Violation> _violations;

    public SourceFile File { get; }
    public Properties Props { get; }
    private readonly IRule _rule;

    public RuleContext(SourceFile file, IRule rule, Properties props, List<Violation> violations)
    {
        File = file;
        _rule = rule;
        Props = props;
        _violations = violations;
    }

    public void Report(int beginLine, int endLine, params object[] args) =>
        AppendViolation(beginLine, endLine, "", "", "", args);

    public void ReportMethod(MethodModel method, params object[] args) =>
        AppendViolation(method.Line, method.EndLine,
            method.Class?.Name ?? "", method.Name, "", args);

    public void ReportClass(ClassModel cls, params object[] args) =>
        AppendViolation(cls.Line, cls.EndLine, cls.Name, "", "", args);

    public void ReportInterface(InterfaceModel iface, params object[] args) =>
        AppendViolation(iface.Line, iface.EndLine, iface.Name, "", "", args);

    private void AppendViolation(int beginLine, int endLine,
        string cls, string method, string function, object[] args)
    {
        _violations.Add(new Violation
        {
            Rule = _rule,
            File = File.Path,
            BeginLine = beginLine,
            EndLine = endLine,
            Description = RenderMessage(_rule.Message, args),
            Args = args,
            Class = cls,
            Method = method,
            Function = function,
            Package = File.Namespace,
            Priority = _rule.Priority,
            RuleSetName = _rule.SetName,
        });
    }

    /// <summary>
    /// Substitutes {0}, {1}, ... placeholders in a phpmd message template.
    /// Integral double values render without a decimal point.
    /// </summary>
    public static string RenderMessage(string tmpl, object[] args)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            tmpl, @"\{(\d+)\}", m =>
            {
                if (int.TryParse(m.Groups[1].Value, out var idx)
                    && idx >= 0 && idx < args.Length)
                    return ToStr(args[idx]);
                return m.Value;
            });
    }

    private static string ToStr(object v) => v switch
    {
        string s => s,
        int n => n.ToString(),
        long l => l.ToString(),
        double d when d == Math.Truncate(d) => ((long)d).ToString(),
        double d => d.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
        _ => v?.ToString() ?? "",
    };

    /// <summary>
    /// Compiles a phpmd-style regex property "(pattern)flags" to a .NET Regex.
    /// Translates flags i/m/s/x; drops u (not supported in .NET).
    /// Returns null if the pattern is empty or invalid.
    /// </summary>
    public static System.Text.RegularExpressions.Regex? CompileRegex(string pat)
    {
        if (string.IsNullOrEmpty(pat)) return null;

        string body = pat, flags = "";
        var m = System.Text.RegularExpressions.Regex.Match(pat, @"^\((.*)\)([imsxu]*)$");
        if (m.Success)
        {
            body = m.Groups[1].Value;
            flags = m.Groups[2].Value.Replace("u", "");
        }

        if (flags.Length > 0)
            body = "(?" + flags + ")" + body;

        try { return new System.Text.RegularExpressions.Regex(body); }
        catch { return null; }
    }

    /// <summary>
    /// Sorts violations by file then begin line (phpmd ordering).
    /// </summary>
    public static void SortViolations(List<Violation> vs) =>
        vs.Sort((a, b) =>
        {
            int fc = string.Compare(a.File, b.File, StringComparison.Ordinal);
            return fc != 0 ? fc : a.BeginLine.CompareTo(b.BeginLine);
        });
}
