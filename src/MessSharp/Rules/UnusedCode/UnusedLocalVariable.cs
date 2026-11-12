using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// Reports local variables that are declared/assigned but never read.
/// Port of messgo's UnusedLocalVariable; C# adaptations:
///   - `_ = expr` discards are excluded by name
///   - `out var x` declarations are included (they can be unused too)
///   - `using` resources are excluded: disposal at scope exit is a use
///   - expression-bodied members checked via BodyAnalysis.EffectiveBody
///   - same name reported only once per method (like messgo's dedup)
///   - `exceptions` property: comma-separated list of names to skip
/// </summary>
public sealed class UnusedLocalVariableRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        var body = BodyAnalysis.EffectiveBody(method);
        if (body == null) return;

        var exceptions = ctx.Props.Set("exceptions", nullPolicy: PropertyNullPolicy.Empty);
        var disposed = BodyAnalysis.UsingResourceVariables(body);
        var locals = BodyAnalysis.LocalVariables(body).Where(l => !disposed.Contains(l));
        var reads = BodyAnalysis.IdentReads(body);

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, line) in locals)
        {
            if (reads.Contains(name)) continue;
            if (reported.Contains(name)) continue;
            if (exceptions.Contains(name)) continue;
            reported.Add(name);
            ctx.Report(line, line, name);
        }
    }

}
