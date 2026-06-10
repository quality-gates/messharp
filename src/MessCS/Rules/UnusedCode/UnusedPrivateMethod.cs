using MessCS.Model;
using MessCS.Rule;

namespace MessCS.Rules.UnusedCode;

/// <summary>
/// Reports private methods (non-constructor) that are never referenced within
/// the file. References include: direct calls, method-group references, and
/// nameof(MethodName) — all collected by the shared selector scan.
/// </summary>
public sealed class UnusedPrivateMethodRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        var used = UnusedPrivateFieldRule.CollectUsedNames(ctx.File);
        foreach (var method in cls.Methods)
        {
            if (method.Exported) continue;
            if (method.IsConstructor) continue;
            if (used.Contains(method.Name)) continue;
            ctx.ReportMethod(method, method.Name);
        }
    }
}
