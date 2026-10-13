using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>Reports unresolved operations independently of known action findings.</summary>
public sealed class UnknownEffectRule : BaseRule, IMethodRule, IFileRule
{
    public void Apply(RuleContext ctx, MethodModel method) => Finding.ReportAll(ctx, method,
        CallableGraph.For(ctx).Summary(method).Findings(EffectKind.Unknown));

    public void Apply(RuleContext ctx)
    {
        foreach (var method in CallableGraph.For(ctx).ExtraMethods) Apply(ctx, method);
    }
}
