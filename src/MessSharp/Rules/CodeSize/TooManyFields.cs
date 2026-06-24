using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.CodeSize;

/// <summary>
/// Reports classes with more fields than the threshold (strictly greater).
/// Port of messgo's TooManyFields rule; same phpmd message template.
/// </summary>
public sealed class TooManyFieldsRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        int threshold = ctx.Props.Int("maxfields", 15);
        int count = cls.Fields.Count;
        if (count <= threshold) return;

        ctx.ReportClass(cls, cls.NodeType, cls.Name, count, threshold);
    }
}
