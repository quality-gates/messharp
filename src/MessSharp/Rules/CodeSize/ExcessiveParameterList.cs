using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.CodeSize;

/// <summary>
/// Reports methods with more parameters than the threshold.
/// Port of messgo's LongParameterList rule; same phpmd message template.
/// </summary>
public sealed class ExcessiveParameterListRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        int threshold = ctx.Props.Int("minimum", 10);
        int count = method.Parameters.Count;
        if (count < threshold) return;

        string kind = method.IsConstructor ? "constructor" : "method";
        ctx.ReportMethod(method, kind, method.Name, count, threshold);
    }
}
