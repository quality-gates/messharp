using MessCS.Model;
using MessCS.Rule;
using MetricsCalc = MessCS.Metrics.Metrics;

namespace MessCS.Rules.CodeSize;

/// <summary>
/// Reports methods whose line count exceeds a threshold.
/// Port of messgo's LongMethod rule; same phpmd message template.
/// </summary>
public sealed class ExcessiveMethodLengthRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        int threshold = ctx.Props.Int("minimum", 100);
        bool ignoreWhitespace = ctx.Props.Bool("ignore-whitespace", false);

        int loc = ignoreWhitespace
            ? MetricsCalc.EffectiveLinesOfCode(method.Node, method.File.Source)
            : method.EndLine - method.Line + 1;

        if (loc < threshold) return;

        string kind = method.IsConstructor ? "constructor" : "method";
        ctx.ReportMethod(method, kind, method.Name, loc, threshold);
    }
}
