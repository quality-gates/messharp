using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Strict variant of ImplicitInput: flags an instance method that reads the
/// fields, properties or primary constructor parameters of its own instance.
/// </summary>
public sealed class ImplicitInstanceInputRule : BaseRule, IMethodRule, IFileRule
{
    public void Apply(RuleContext ctx, MethodModel method) =>
        Finding.ReportAll(ctx, method, CallableGraph.For(ctx).Summary(method).Findings(EffectKind.InstanceInput));

    public void Apply(RuleContext ctx)
    {
        foreach (var method in CallableGraph.For(ctx).ExtraMethods) Apply(ctx, method);
    }
}
