using MessSharp.Model;

namespace MessSharp.Rule;

/// <summary>
/// Dispatches rules over a SourceFile's artifacts. Mirrors messgo's
/// rule.Analyze / applyRule dispatch logic.
/// </summary>
public static class Engine
{
    /// <summary>
    /// Analyzes a set of files together, so partial types split across
    /// files are seen whole by type-scoped rules.
    /// </summary>
    public static List<Violation> AnalyzeAll(IReadOnlyList<SourceFile> files, IEnumerable<RuleSet> sets, bool strict = false)
    {
        var partials = new PartialTypeIndex(files);
        var violations = new List<Violation>();
        foreach (var file in files)
            violations.AddRange(Analyze(file, sets, strict, partials));
        return violations;
    }

    public static List<Violation> Analyze(SourceFile file, IEnumerable<RuleSet> sets, bool strict = false,
        PartialTypeIndex? partials = null)
    {
        var violations = new List<Violation>();
        foreach (var set in sets)
        {
            foreach (var rule in set.Rules)
            {
                var props = rule is BaseRule br ? br.RuleProps : Properties.Empty;
                var ctx = new RuleContext(file, rule, props, violations, partials);
                ApplyRule(ctx, rule, file);
            }
        }
        return strict ? violations : SuppressionFilter.Filter(violations, file);
    }

    /// <summary>
    /// Analyzes a parsed SourceFile against a flat collection of rules,
    /// applying suppression filtering unless strict. Encapsulates rule
    /// container construction so callers need no XML-modelled RuleSet.
    /// </summary>
    public static List<Violation> Analyze(SourceFile file, IEnumerable<IRule> rules, bool strict = false,
        PartialTypeIndex? partials = null)
    {
        return Analyze(file, new[] { new RuleSet { Rules = rules.ToList() } }, strict, partials);
    }

    /// <summary>
    /// Analyzes C# source text against a flat collection of rules. Parses the
    /// source via ModelBuilder, dispatches every rule kind, filters suppressed
    /// violations (unless strict), and returns the processed violations.
    /// </summary>
    public static List<Violation> Analyze(string source, IEnumerable<IRule> rules, bool strict = false,
        PartialTypeIndex? partials = null)
    {
        return Analyze(ModelBuilder.Parse("test.cs", source), rules, strict, partials);
    }

    private static void ApplyRule(RuleContext ctx, IRule rule, SourceFile file)
    {
        if (rule is IFileRule fr)
            fr.Apply(ctx);

        if (rule is IClassRule cr)
        {
            foreach (var cls in file.Classes)
            {
                ctx.CurrentPackage = cls.Namespace;
                cr.Apply(ctx, cls);
            }
        }

        if (rule is IInterfaceRule ir)
        {
            foreach (var iface in file.Interfaces)
            {
                ctx.CurrentPackage = iface.Namespace;
                ir.Apply(ctx, iface);
            }
        }

        if (rule is IMethodRule mr)
            ApplyMethodRule(ctx, mr, file);

        if (rule is IFunctionRule)
        {
            // C# has no free functions at the class level.
            // IFunctionRule exists for parity but rarely fires.
        }
    }

    private static void ApplyMethodRule(RuleContext ctx, IMethodRule mr, SourceFile file)
    {
        foreach (var m in file.AllMethods)
        {
            ctx.CurrentPackage = m.Namespace;
            mr.Apply(ctx, m);
        }

        foreach (var iface in file.Interfaces)
        {
            ctx.CurrentPackage = iface.Namespace;
            foreach (var m in iface.Methods)
                mr.Apply(ctx, m);
        }
    }
}
