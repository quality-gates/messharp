using MessSharp.Model;

namespace MessSharp.Rule;

/// <summary>
/// Handed to each rule during analysis. Rules append violations via helpers.
/// </summary>
public sealed class RuleContext
{
    private readonly List<Violation> _violations;

    public SourceFile File { get; }
    public Properties Props { get; }
    public string CurrentPackage { get; set; }

    /// <summary>
    /// Parts of partial types across every analysed file, for rules whose
    /// scope is a whole type rather than a single file.
    /// </summary>
    public PartialTypeIndex Partials { get; }

    internal AnalysisFacts Facts { get; }
    private readonly IRule _rule;

    public RuleContext(SourceFile file, IRule rule, Properties props, List<Violation> violations,
        PartialTypeIndex? partials = null)
        : this(file, rule, props, violations, partials, new AnalysisFacts())
    {
    }

    internal RuleContext(SourceFile file, IRule rule, Properties props, List<Violation> violations,
        PartialTypeIndex? partials, AnalysisFacts facts)
    {
        File = file;
        _rule = rule;
        Props = props;
        _violations = violations;
        CurrentPackage = file.Namespace;
        Partials = partials ?? PartialTypeIndex.Empty;
        Facts = facts;
    }

    public void Report(int beginLine, int endLine, params object[] args) =>
        AppendViolation(beginLine, endLine, "", "", "", CurrentPackage, args);

    public void ReportMethod(MethodModel method, params object[] args) =>
        AppendViolation(method.Line, method.EndLine,
            method.DeclaringTypeName, method.Name, "", method.Namespace, args);

    internal void ReportMethodAt(MethodModel method, int beginLine, int endLine, params object[] args) =>
        AppendViolation(beginLine, endLine,
            method.DeclaringTypeName, method.Name, "", method.Namespace, args);

    public void ReportClass(ClassModel cls, params object[] args) =>
        AppendViolation(cls.Line, cls.EndLine, cls.Name, "", "", cls.Namespace, args);

    public void ReportField(ClassModel cls, FieldModel field, params object[] args) =>
        AppendViolation(field.Line, field.EndLine > 0 ? field.EndLine : field.Line,
            cls.Name, "", "", cls.Namespace, args);

    public void ReportField(FieldModel field, params object[] args) =>
        AppendViolation(field.Line, field.EndLine > 0 ? field.EndLine : field.Line,
            "", "", "", CurrentPackage, args);

    public void ReportInterface(InterfaceModel iface, params object[] args) =>
        AppendViolation(iface.Line, iface.EndLine, iface.Name, "", "", iface.Namespace, args);

    private void AppendViolation(int beginLine, int endLine,
        string cls, string method, string function, string package, object[] args)
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
            Package = package,
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
    /// Sorts violations by file then begin line (phpmd ordering).
    /// </summary>
    public static void SortViolations(List<Violation> vs) => global::MessSharp.Report.Report.SortViolations(vs);
}
