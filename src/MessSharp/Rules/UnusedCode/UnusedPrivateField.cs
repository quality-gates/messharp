using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// Reports private fields that are never read within the file or within the
/// other files' parts of the same partial type.
/// Port of messgo's UnusedPrivateField; adapted to C# (private modifier,
/// `this.Name` member-access, struct-literal key, nameof(x) all count as use).
/// </summary>
public sealed class UnusedPrivateFieldRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        var used = UsedMemberNames.Collect(ctx, cls);
        foreach (var field in cls.Fields)
        {
            if (!field.IsPrivate) continue;
            if (field.Name == "_") continue;
            if (used.Contains(field.Name)) continue;
            ctx.ReportField(cls, field, field.Name);
        }
    }
}
