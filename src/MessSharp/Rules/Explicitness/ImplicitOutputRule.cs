using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>Reports known writes that leave through shared storage or ambient operations.</summary>
public sealed class ImplicitOutputRule : BaseRule, IMethodRule, IFileRule
{
    public void Apply(RuleContext ctx, MethodModel method) => Finding.ReportAll(ctx, method,
        CallableGraph.For(ctx).Summary(method).Outputs(method)
            .Concat(ctx.Props.Bool("receiver-effects-established", false) ? []
                : CallableGraph.For(ctx).Summary(method).Findings(EffectKind.InstanceOutput)));

    public void Apply(RuleContext ctx)
    {
        foreach (var method in CallableGraph.For(ctx).ExtraMethods) Apply(ctx, method);
    }
}
