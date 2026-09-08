using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.CleanCode;

/// <summary>
/// Flags invocations of static methods on other named classes.
/// Skips: calls on the class's own type name, exception classes (property
/// `exceptions`), enum/const member access (non-invocation), extension
/// method style calls.
/// Based on phpmd's StaticAccess rule.
/// </summary>
public sealed class StaticAccessRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        var body = method.EffectiveBody;
        if (body == null) return;

        var exceptions = SplitList(ctx.Props.Str("exceptions", ""));
        var ignorePattern = RuleContext.CompileRegex(ctx.Props.Str("ignorepattern", ""));
        if (ignorePattern != null && ignorePattern.IsMatch(method.Name)) return;

        var ownClass = method.Class?.Name ?? "";
        foreach (var invocation in body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            CheckInvocation(ctx, method.Name, invocation, ownClass, exceptions);
    }

    private static void CheckInvocation(RuleContext ctx, string methodName,
        InvocationExpressionSyntax invocation, string ownClass, HashSet<string> exceptions)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return;
        if (!memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)) return;

        var targetClassName = GetTargetClassName(memberAccess.Expression);
        if (targetClassName == null) return;
        if (IsSkipped(targetClassName, ownClass, exceptions)) return;

        var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        ctx.Report(line, line, targetClassName, methodName);
    }

    private static string? GetTargetClassName(ExpressionSyntax expression) =>
        expression switch
        {
            SimpleNameSyntax simpleName => simpleName.Identifier.Text,
            MemberAccessExpressionSyntax qualifiedName => qualifiedName.Name.Identifier.Text,
            _ => null,
        };

    private static bool IsSkipped(string targetClassName, string ownClass, HashSet<string> exceptions)
    {
        if (targetClassName.Length == 0 || char.IsLower(targetClassName[0])) return true;
        if (targetClassName == ownClass) return true;
        return exceptions.Contains(targetClassName);
    }

    private static HashSet<string> SplitList(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return new HashSet<string>(StringComparer.Ordinal);
        return new HashSet<string>(
            val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.Ordinal);
    }
}
