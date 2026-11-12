using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Naming;

/// <summary>
/// Reports methods whose names are shorter than the configured minimum length.
/// Port of phpmd's ShortMethodName rule.
/// </summary>
public sealed class ShortMethodNameRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.IsConstructor)
            return;

        int min = ctx.Props.Int("minimum", 3);
        if (method.Name.Length >= min)
            return;

        var exceptions = ctx.Props.Set("exceptions");

        if (exceptions.Contains(method.Name))
            return;

        string className = method.DeclaringTypeName;
        ctx.ReportMethod(method, className, method.Name, min);
    }
}
