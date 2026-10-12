using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>Reports unresolved operations independently of known action findings.</summary>
public sealed class UnknownEffectRule : BaseRule, IMethodRule, IFileRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        var summary = CallableGraph.For(ctx).Summary(method);
        var receiver = summary.Findings(EffectKind.InstanceInput).Concat(summary.Findings(EffectKind.InstanceOutput))
            .Where(_ => !ctx.Props.Bool("receiver-effects-established", false))
            .Select(f => new Finding(f.Site, "receiver state requires the strict policy"));
        Finding.ReportAll(ctx, method, summary.Findings(EffectKind.Unknown).Concat(receiver));
    }

    public void Apply(RuleContext ctx)
    {
        foreach (var method in CallableGraph.For(ctx).ExtraMethods) Apply(ctx, method);
    }
}
