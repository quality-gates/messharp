using MessCS.Model;
using MessCS.Rule;

namespace MessCS.Rules.Naming;

/// <summary>
/// Reports classes and interfaces whose name is shorter than the configured
/// minimum length. Port of phpmd's ShortClassName rule.
/// </summary>
public sealed class ShortClassNameRule : BaseRule, IClassRule, IInterfaceRule
{
    private void Check(RuleContext ctx, string name, int line, int endLine)
    {
        int min = ctx.Props.Int("minimum", 3);
        if (name.Length >= min)
            return;

        var exceptions = SplitList(ctx.Props.Str("exceptions", ""));
        if (exceptions.Contains(name))
            return;

        ctx.Report(line, endLine, name, min);
    }

    public void Apply(RuleContext ctx, ClassModel cls) =>
        Check(ctx, cls.Name, cls.Line, cls.EndLine);

    public void Apply(RuleContext ctx, InterfaceModel iface) =>
        Check(ctx, iface.Name, iface.Line, iface.EndLine);

    private static HashSet<string> SplitList(string s) =>
        new(s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.Ordinal);
}
