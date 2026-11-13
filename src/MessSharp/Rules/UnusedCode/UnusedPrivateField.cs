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
        var candidates = cls.Fields.Where(field => field.IsPrivate && field.Name != "_").ToList();
        if (candidates.Count == 0) return;
        var used = UsedMemberNames.Collect(ctx, cls);
        foreach (var field in candidates)
        {
            if (used.Contains(field.Name)) continue;
            ctx.ReportField(cls, field, field.Name);
        }
    }
}
