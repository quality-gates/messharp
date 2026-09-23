using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Flags data that leaves a method other than through its return value:
/// writes to the class's static state or changes to the objects it holds,
/// changes to argument objects, writes to ref and out parameters, and
/// writes to the console, debug trace, file system and environment.
/// </summary>
public sealed class ImplicitOutputRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.EffectiveBody is not { } body) return;
        Finding.ReportAll(ctx, method,
            StaticWrites(ctx, method, body).Concat(ParameterWrites(method, body)).Concat(AmbientWrites(body)));
    }

    private static IEnumerable<Finding> StaticWrites(RuleContext ctx, MethodModel method, SyntaxNode body)
    {
        if (method.Class is not { } cls || method.IsStaticConstructor()) return [];
        var names = StateNames.StaticMembers(ClassState.Gather(ctx, cls).Statics, cls.Name);
        return StateAccessCollector.Collect(body, names.Resolve)
            .Where(a => a.Kind != AccessKind.Read)
            .Select(a => Finding.FromAccess(a, "static member"));
    }

    /// <summary>
    /// A new value for a by-value parameter stays in the method, so only
    /// changes to the argument object and writes through ref or out count.
    /// </summary>
    private static IEnumerable<Finding> ParameterWrites(MethodModel method, SyntaxNode body)
    {
        var modes = method.Parameters.DistinctBy(p => p.Name)
            .ToDictionary(p => p.Name, PassingMode, StringComparer.Ordinal);
        var names = StateNames.Parameters(modes.Keys.ToHashSet(StringComparer.Ordinal));
        foreach (var access in StateAccessCollector.Collect(body, names.Resolve))
        {
            if (access.Kind == AccessKind.Change)
                yield return new Finding(access.Site, $"changes argument '{access.Name}'");
            else if (access.Kind == AccessKind.Write && modes[access.Name] is { } mode)
                yield return new Finding(access.Site, $"writes {mode} parameter '{access.Name}'");
        }
    }

    private static string? PassingMode(ParameterModel parameter)
    {
        if (parameter.IsOut) return "out";
        return parameter.Node.Modifiers.Any(SyntaxKind.RefKeyword) ? "ref" : null;
    }

    private static IEnumerable<Finding> AmbientWrites(SyntaxNode body) =>
        AmbientApi.Outputs.UsesIn(body).Select(use => new Finding(use.Site, "uses " + use.Name));
}
