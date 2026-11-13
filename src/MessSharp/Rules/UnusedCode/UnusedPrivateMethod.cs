using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// Reports private methods (non-constructor) that are never referenced within
/// the file or within the other files' parts of the same partial type. References include: direct calls, method-group references, and
/// nameof(MethodName) — all collected by the shared selector scan.
/// </summary>
public sealed class UnusedPrivateMethodRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        var candidates = cls.Methods
            .Where(method => method.IsPrivate && !method.IsExplicitInterfaceImplementation && !method.IsConstructor)
            .ToList();
        if (candidates.Count == 0) return;
        var used = UsedMemberNames.Collect(ctx, cls);
        foreach (var method in candidates)
        {
            if (used.Contains(method.Name)) continue;
            ctx.ReportMethod(method, method.Name);
        }
    }
}
