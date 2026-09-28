using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Flags data that enters a method other than through its parameters: reads
/// of the class's mutable static state, and reads of the clock, environment,
/// console, file system and random sources.
/// </summary>
public sealed class ImplicitInputRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        var effects = MethodEffects.For(ctx, method);
        Finding.ReportAll(ctx, method, StaticReads(effects).Concat(effects.AmbientInputs));
    }

    private static IEnumerable<Finding> StaticReads(MethodEffects effects) =>
        effects.Statics
            .Where(a => a.Kind == AccessKind.Read && effects.MutableStatics.Contains(a.Name))
            .Select(a => Finding.FromAccess(a, "static member"));
}
