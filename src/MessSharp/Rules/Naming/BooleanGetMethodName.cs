using MessSharp.Model;
using MessSharp.Rule;
using System.Text.RegularExpressions;

namespace MessSharp.Rules.Naming;

/// <summary>
/// Reports methods named GetX that return bool — convention is IsX or HasX.
/// When <c>checkParameterizedMethods=false</c> (default), methods with
/// parameters are exempt. Port of phpmd's BooleanGetMethodName rule.
/// </summary>
public sealed class BooleanGetMethodNameRule : BaseRule, IMethodRule
{
    private static readonly Regex GetterPattern =
        new(@"^[Gg]et", RegexOptions.Compiled);

    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (!GetterPattern.IsMatch(method.Name))
            return;

        if (!BooleanReturnType.Matches(method.ReturnType))
            return;

        bool checkParameterized = ctx.Props.Bool("checkParameterizedMethods", false);
        if (!checkParameterized && method.Parameters.Count > 0)
            return;

        ctx.ReportMethod(method, method.Name);
    }
}

/// <summary>
/// Classifies a return type's source text as boolean. A <c>T?</c> suffix or a
/// <c>[System.]Nullable&lt;T&gt;</c> wrapper is unwrapped first, so nullable
/// booleans count as booleans (as BooleanArgumentFlag does).
/// </summary>
internal static class BooleanReturnType
{
    private static readonly Regex NullableWrapper =
        new(@"^(?:System\s*\.\s*)?Nullable\s*<(?<inner>.*)>$", RegexOptions.Compiled);

    public static bool Matches(string returnType)
    {
        var t = UnwrapNullable(returnType.Trim());
        return t.Equals("bool", StringComparison.OrdinalIgnoreCase)
            || t.Equals("boolean", StringComparison.OrdinalIgnoreCase)
            || t.Equals("System.Boolean", StringComparison.OrdinalIgnoreCase);
    }

    private static string UnwrapNullable(string type)
    {
        if (type.EndsWith('?'))
            return type[..^1].TrimEnd();

        var match = NullableWrapper.Match(type);
        return match.Success ? match.Groups["inner"].Value.Trim() : type;
    }
}
