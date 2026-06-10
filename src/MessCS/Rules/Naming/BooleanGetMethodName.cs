using MessCS.Model;
using MessCS.Rule;
using System.Text.RegularExpressions;

namespace MessCS.Rules.Naming;

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

        if (!ReturnsBool(method.ReturnType))
            return;

        bool checkParameterized = ctx.Props.Bool("checkParameterizedMethods", false);
        if (!checkParameterized && method.Parameters.Count > 0)
            return;

        ctx.ReportMethod(method, method.Name);
    }

    private static bool ReturnsBool(string returnType)
    {
        var t = returnType.Trim();
        return t.Equals("bool", StringComparison.OrdinalIgnoreCase)
            || t.Equals("boolean", StringComparison.OrdinalIgnoreCase);
    }
}
