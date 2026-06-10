using MessCS.Model;
using MessCS.Rule;

namespace MessCS.Rules.UnusedCode;

/// <summary>
/// Reports method/constructor parameters that are never referenced in the body.
/// Port of messgo's UnusedFormalParameter; C# adaptations:
///   - `out var` parameters (the parameter itself) still count if referenced
///   - params named `_` are ignored (explicit discard pattern)
///   - expression-bodied members are checked via BodyAnalysis.EffectiveBody
/// </summary>
public sealed class UnusedFormalParameterRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.Parameters.Count == 0) return;
        var body = BodyAnalysis.EffectiveBody(method);
        if (body == null) return;   // abstract / extern / interface declaration

        var reads = BodyAnalysis.IdentReads(body);
        foreach (var p in method.Parameters)
        {
            if (string.IsNullOrEmpty(p.Name) || p.Name == "_") continue;
            if (!reads.Contains(p.Name))
                ctx.Report(p.Line, p.Line, p.Name);
        }
    }
}
