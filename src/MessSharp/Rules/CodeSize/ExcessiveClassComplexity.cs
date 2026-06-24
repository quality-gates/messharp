using MessSharp.Model;
using MessSharp.Rule;
using MetricsCalc = MessSharp.Metrics.Metrics;

namespace MessSharp.Rules.CodeSize;

/// <summary>
/// Reports classes whose Weighted Method Count (sum of cyclomatic complexity
/// of all methods) exceeds a threshold.
/// Port of messgo's WeightedMethodCount rule; same phpmd message template.
/// </summary>
public sealed class ExcessiveClassComplexityRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        int threshold = ctx.Props.Int("maximum", 50);

        int wmc = 0;
        foreach (var m in cls.Methods)
            wmc += MetricsCalc.CyclomaticComplexity(m.Body);

        if (wmc < threshold) return;

        ctx.ReportClass(cls, cls.Name, wmc, threshold);
    }
}
