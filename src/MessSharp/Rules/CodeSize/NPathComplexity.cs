using MessSharp.Model;
using MessSharp.Rule;
using MetricsCalc = MessSharp.Metrics.Metrics;

namespace MessSharp.Rules.CodeSize;

/// <summary>
/// Reports methods whose NPath complexity exceeds a threshold.
/// Port of messgo's NPathComplexity rule; same phpmd message template.
/// </summary>
public sealed class NPathComplexityRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        int threshold = ctx.Props.Int("minimum", 200);
        int npath = MetricsCalc.NPathComplexity(method.Body);
        if (npath < threshold) return;

        string kind = method.IsConstructor ? "constructor" : "method";
        ctx.ReportMethod(method, kind, method.Name, npath, threshold);
    }
}
