using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Naming;

/// <summary>
/// Reports classes and interfaces whose name exceeds the configured maximum
/// length (after stripping configured prefixes/suffixes).
/// Port of phpmd's LongClassName rule.
/// </summary>
public sealed class LongClassNameRule : BaseRule, IClassRule, IInterfaceRule
{
    private void Check(RuleContext ctx, string name, int line, int endLine)
    {
        int max = ctx.Props.Int("maximum", 40);
        var prefixes = SplitList(ctx.Props.Str("subtract-prefixes", ""));
        var suffixes = SplitList(ctx.Props.Str("subtract-suffixes", ""));
        int effectiveLen = LengthWithout(name, prefixes, suffixes);
        if (effectiveLen <= max)
            return;

        ctx.Report(line, endLine, name, max);
    }

    public void Apply(RuleContext ctx, ClassModel cls) =>
        Check(ctx, cls.Name, cls.Line, cls.EndLine);

    public void Apply(RuleContext ctx, InterfaceModel iface) =>
        Check(ctx, iface.Name, iface.Line, iface.EndLine);

    internal static int LengthWithout(string name, List<string> prefixes, List<string> suffixes)
    {
        string effective = name;
        foreach (var p in prefixes)
        {
            if (p.Length > 0 && effective.StartsWith(p, StringComparison.Ordinal))
            {
                effective = effective[p.Length..];
                break;
            }
        }
        foreach (var s in suffixes)
        {
            if (s.Length > 0 && effective.EndsWith(s, StringComparison.Ordinal))
            {
                effective = effective[..^s.Length];
                break;
            }
        }
        return effective.Length;
    }

    private static List<string> SplitList(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
