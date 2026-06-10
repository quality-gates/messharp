using MessCS.Model;
using MessCS.Rule;

namespace MessCS.Rules.CodeSize;

/// <summary>
/// Reports classes with too many non-accessor methods (strictly greater than threshold).
/// Port of messgo's TooManyMethods rule; same phpmd message template.
/// By default ignores methods matching (^(set|get|is|has|with))i.
/// </summary>
public sealed class TooManyMethodsRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        int threshold = ctx.Props.Int("maxmethods", 25);
        var re = RuleContext.CompileRegex(
            ctx.Props.Str("ignorepattern", "(^(set|get|is|has|with))i"));

        int count = 0;
        foreach (var m in cls.Methods)
        {
            if (re != null && re.IsMatch(m.Name)) continue;
            count++;
        }

        if (count <= threshold) return;

        ctx.ReportClass(cls, cls.NodeType, cls.Name, count, threshold);
    }
}
