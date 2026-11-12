using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Naming;

/// <summary>
/// Reports fields, parameters, and local variables whose names exceed the
/// configured maximum length (after stripping configured prefixes/suffixes).
/// Port of phpmd's LongVariable rule.
/// </summary>
public sealed class LongVariableRule : BaseRule, IClassRule, IMethodRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        int max = ctx.Props.Int("maximum", 20);
        var prefixes = ctx.Props.List("subtract-prefixes");
        var suffixes = ctx.Props.List("subtract-suffixes");
        foreach (var f in cls.Fields)
            CheckName(ctx, f.Name, f.Line, max, prefixes, suffixes);
    }

    public void Apply(RuleContext ctx, MethodModel method)
    {
        int max = ctx.Props.Int("maximum", 20);
        var prefixes = ctx.Props.List("subtract-prefixes");
        var suffixes = ctx.Props.List("subtract-suffixes");

        foreach (var p in method.Parameters)
            CheckName(ctx, p.Name, p.Line, max, prefixes, suffixes);

        if (method.EffectiveBody != null)
        {
            foreach (var (name, line, _) in ShortVariableRule.CollectLocals(method.EffectiveBody))
                CheckName(ctx, name, line, max, prefixes, suffixes);
        }
    }

    private static void CheckName(RuleContext ctx, string name, int line,
        int max, IReadOnlyList<string> prefixes, IReadOnlyList<string> suffixes)
    {
        int effective = LongClassNameRule.LengthWithout(name, prefixes, suffixes);
        if (effective <= max) return;
        ctx.Report(line, line, name, max);
    }

}
