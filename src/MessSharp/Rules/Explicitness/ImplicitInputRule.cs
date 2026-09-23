using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;

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
        if (method.EffectiveBody is not { } body) return;
        Finding.ReportAll(ctx, method, StaticReads(ctx, method, body).Concat(AmbientReads(body)));
    }

    private static IEnumerable<Finding> StaticReads(RuleContext ctx, MethodModel method, SyntaxNode body)
    {
        if (method.Class is not { } cls || method.IsStaticConstructor()) return [];
        var names = StateNames.StaticMembers(ClassState.Gather(ctx, cls).MutableStatics, cls.Name);
        return StateAccessCollector.Collect(body, names.Resolve)
            .Where(a => a.Kind == AccessKind.Read)
            .Select(a => Finding.FromAccess(a, "static member"));
    }

    private static IEnumerable<Finding> AmbientReads(SyntaxNode body) =>
        AmbientApi.Inputs.UsesIn(body).Select(use => new Finding(use.Site, "uses " + use.Name))
            .Concat(AmbientApi.UnseededRandoms(body).Select(site => new Finding(site, "uses new Random()")));
}
