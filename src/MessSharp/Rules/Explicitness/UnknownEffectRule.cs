using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>Reports unresolved operations independently of known action findings.</summary>
public sealed class UnknownEffectRule : BaseRule, IMethodRule, IFileRule
{
    public void Apply(RuleContext ctx, MethodModel method) => Finding.ReportAll(ctx, method,
        owner => CallableGraph.For(ctx).Summary(owner).Findings(EffectKind.Unknown));

    public void Apply(RuleContext ctx)
    {
        foreach (var method in CallableGraph.For(ctx).ExtraMethods) Apply(ctx, method);
    }
}
