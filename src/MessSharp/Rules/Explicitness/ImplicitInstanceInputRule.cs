using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Strict variant of ImplicitInput: flags an instance method that reads the
/// fields, properties or primary constructor parameters of its own instance.
/// </summary>
public sealed class ImplicitInstanceInputRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method) =>
        Finding.ReportAll(ctx, method, InstanceStateAccesses.Collect(ctx, method)
            .Where(a => a.Kind == AccessKind.Read)
            .Select(a => Finding.FromAccess(a, "member")));
}
