using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.CodeSize;

/// <summary>
/// Reports classes with too many public non-accessor methods (strictly greater than threshold).
/// Port of messgo's TooManyPublicMethods rule; same phpmd message template.
/// By default ignores methods matching (^(set|get|is|has|with))i.
/// </summary>
public sealed class TooManyPublicMethodsRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        int threshold = ctx.Props.Int("maxmethods", 10);
        var re = RuleContext.CompileRegex(
            ctx.Props.Str("ignorepattern", "(^(set|get|is|has|with))i"));

        int count = 0;
        foreach (var m in cls.Methods)
        {
            if (!m.Exported) continue;
            if (re != null && re.IsMatch(m.Name)) continue;
            count++;
        }

        if (count <= threshold) return;

        ctx.ReportClass(cls, cls.NodeType, cls.Name, count, threshold);
    }
}
