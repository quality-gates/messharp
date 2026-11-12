using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.CleanCode;

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

        string image = method.DeclaringTypeName.Length > 0
            ? method.DeclaringTypeName + "::" + method.Name
            : method.Name;

        foreach (var param in method.Parameters)
        {
            if (IsBoolType(param.Type) && param.Name.Length > 0)
                ctx.Report(param.Line, param.Line, image, param.Name);
        }
    }

    private static bool IsExcluded(RuleContext ctx, MethodModel method)
    {
        var exceptions = ctx.Props.Set("exceptions", nullPolicy: PropertyNullPolicy.Empty);
        if (exceptions.Contains(method.DeclaringTypeName)) return true;
        var ignorePattern = ctx.Props.Regex("ignorepattern");
        return ignorePattern != null && ignorePattern.IsMatch(method.Name);
    }

    private static bool IsBoolType(string type)
    {
        var t = type.Trim();
        return t == "bool" || t == "Boolean" || t == "System.Boolean"
            || t == "bool?" || t == "Boolean?" || t == "System.Boolean?"
            || t == "Nullable<bool>" || t == "Nullable<Boolean>";
    }

}
