using MessCS.Model;
using MessCS.Rule;
using MetricsCalc = MessCS.Metrics.Metrics;

namespace MessCS.Rules.CodeSize;

/// <summary>
/// Reports methods whose cyclomatic complexity exceeds a threshold.
/// Port of messgo's CyclomaticComplexity rule; same phpmd message template.
/// </summary>
public sealed class CyclomaticComplexityRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        int threshold = ctx.Props.Int("reportLevel", 10);
        int ccn = MetricsCalc.CyclomaticComplexity(method.Body);
        if (ccn < threshold) return;

        string kind = "method";
        ctx.ReportMethod(method, kind, method.Name, ccn, threshold);
    }
}
