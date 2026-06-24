using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.CodeSize;

/// <summary>
/// Reports classes with too many public methods and attributes combined.
/// Port of messgo's ExcessivePublicCount rule; same phpmd message template.
/// </summary>
public sealed class ExcessivePublicCountRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        int threshold = ctx.Props.Int("minimum", 45);

        int count = 0;
        foreach (var m in cls.Methods)
        {
            if (m.Exported) count++;
        }
        foreach (var f in cls.Fields)
        {
            if (f.Exported) count++;
        }

        if (count < threshold) return;

        ctx.ReportClass(cls, cls.NodeType, cls.Name, count, threshold);
    }
}
