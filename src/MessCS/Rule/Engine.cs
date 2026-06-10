using MessCS.Model;

namespace MessCS.Rule;

/// <summary>
/// Dispatches rules over a SourceFile's artifacts. Mirrors messgo's
/// rule.Analyze / applyRule dispatch logic.
/// </summary>
public static class Engine
{
    public static List<Violation> Analyze(SourceFile file, IEnumerable<RuleSet> sets)
    {
        var violations = new List<Violation>();
        foreach (var set in sets)
        {
            foreach (var rule in set.Rules)
            {
                var props = rule is BaseRule br ? br.RuleProps : Properties.Empty;
                var ctx = new RuleContext(file, rule, props, violations);
                ApplyRule(ctx, rule, file);
            }
        }
        return violations;
    }

    private static void ApplyRule(RuleContext ctx, IRule rule, SourceFile file)
    {
        if (rule is IFileRule fr)
            fr.Apply(ctx);

        if (rule is IClassRule cr)
        {
            foreach (var cls in file.Classes)
                cr.Apply(ctx, cls);
        }

        if (rule is IInterfaceRule ir)
        {
            foreach (var iface in file.Interfaces)
                ir.Apply(ctx, iface);
        }

        if (rule is IMethodRule mr)
            ApplyMethodRule(ctx, mr, file);

        if (rule is IFunctionRule fnr)
        {
            // C# has no free functions at the class level.
            // IFunctionRule exists for parity but rarely fires.
        }
    }

    private static void ApplyMethodRule(RuleContext ctx, IMethodRule mr, SourceFile file)
    {
        foreach (var m in file.AllMethods)
            mr.Apply(ctx, m);

        foreach (var iface in file.Interfaces)
        {
            foreach (var m in iface.Methods)
                mr.Apply(ctx, m);
        }
    }
}
