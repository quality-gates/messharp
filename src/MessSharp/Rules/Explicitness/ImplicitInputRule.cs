using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>Reports known reads that do not enter through arguments.</summary>
public sealed class ImplicitInputRule : BaseRule, IMethodRule, IFileRule
{
    public void Apply(RuleContext ctx, MethodModel method) => Finding.ReportAll(ctx, method,
        CallableGraph.For(ctx).Summary(method).Findings(EffectKind.Input));

    public void Apply(RuleContext ctx)
    {
        foreach (var method in CallableGraph.For(ctx).ExtraMethods) Apply(ctx, method);
    }
}
