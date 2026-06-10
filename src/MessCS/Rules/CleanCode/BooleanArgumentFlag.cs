using MessCS.Model;
using MessCS.Rule;

namespace MessCS.Rules.CleanCode;

/// <summary>
/// Flags boolean parameters in public method signatures.
/// A boolean flag argument is a reliable indicator for a violation of the
/// Single Responsibility Principle (SRP).
/// Port of phpmd's BooleanArgumentFlag rule.
/// </summary>
public sealed class BooleanArgumentFlagRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (!method.Exported) return;
        if (IsExcluded(ctx, method)) return;

        string image = method.Class != null
            ? method.Class.Name + "::" + method.Name
            : method.Name;

        foreach (var param in method.Parameters)
        {
            if (IsBoolType(param.Type) && param.Name.Length > 0)
                ctx.Report(param.Line, param.Line, image, param.Name);
        }
    }

    private static bool IsExcluded(RuleContext ctx, MethodModel method)
    {
        var exceptions = SplitList(ctx.Props.Str("exceptions", ""));
        if (exceptions.Contains(method.Class?.Name ?? "")) return true;
        var ignorePattern = RuleContext.CompileRegex(ctx.Props.Str("ignorepattern", ""));
        return ignorePattern != null && ignorePattern.IsMatch(method.Name);
    }

    private static bool IsBoolType(string type)
    {
        var t = type.Trim();
        return t == "bool" || t == "Boolean"
            || t == "bool?" || t == "Boolean?"
            || t == "Nullable<bool>" || t == "Nullable<Boolean>";
    }

    private static HashSet<string> SplitList(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return new HashSet<string>(StringComparer.Ordinal);
        return new HashSet<string>(
            val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.Ordinal);
    }
}
