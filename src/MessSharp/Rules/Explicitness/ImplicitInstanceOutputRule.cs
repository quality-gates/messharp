using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Strict variant of ImplicitOutput: flags an instance method that writes the
/// fields or properties of its own instance, or changes the objects they hold.
/// </summary>
public sealed class ImplicitInstanceOutputRule : BaseRule, IMethodRule, IFileRule
{
    public void Apply(RuleContext ctx, MethodModel method) =>
        Finding.ReportAll(ctx, method, CallableGraph.For(ctx).Summary(method).Findings(EffectKind.InstanceOutput));

    public void Apply(RuleContext ctx)
    {
        foreach (var method in CallableGraph.For(ctx).ExtraMethods) Apply(ctx, method);
    }
}
