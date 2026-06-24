using MessSharp.Model;
using MessSharp.Rule;
using MetricsCalc = MessSharp.Metrics.Metrics;

namespace MessSharp.Rules.CodeSize;

/// <summary>
/// Reports classes whose line count exceeds a threshold.
/// Port of messgo's LongClass rule; same phpmd message template.
/// </summary>
public sealed class ExcessiveClassLengthRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        int threshold = ctx.Props.Int("minimum", 1000);
        bool ignoreWhitespace = ctx.Props.Bool("ignore-whitespace", false);

        int loc = ClassLoc(cls, ignoreWhitespace);
        if (loc < threshold) return;

        ctx.ReportClass(cls, cls.Name, loc, threshold);
    }

    private static int ClassLoc(ClassModel cls, bool ignoreWhitespace)
    {
        if (ignoreWhitespace)
            return MetricsCalc.EffectiveLinesOfCode(cls.Node, cls.File.Source);
        return cls.EndLine - cls.Line + 1;
    }
}
