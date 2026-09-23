using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Strict variant of ImplicitOutput: flags an instance method that writes the
/// fields or properties of its own instance, or changes the objects they hold.
/// </summary>
public sealed class ImplicitInstanceOutputRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method) =>
        Finding.ReportAll(ctx, method, InstanceStateAccesses.Collect(ctx, method)
            .Where(a => a.Kind != AccessKind.Read)
            .Select(a => Finding.FromAccess(a, "member")));
}
