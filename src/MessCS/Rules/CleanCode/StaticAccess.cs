using MessCS.Model;
using MessCS.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Rules.CleanCode;

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
        if (method.Body == null) return;

        var exceptions = SplitList(ctx.Props.Str("exceptions", ""));
        var ignorePattern = RuleContext.CompileRegex(ctx.Props.Str("ignorepattern", ""));
        var ownClass = method.Class?.Name ?? "";

        if (ignorePattern != null && ignorePattern.IsMatch(method.Name)) return;

        foreach (var invocation in method.Body.DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) continue;
            if (!memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)) continue;

            // The target must be a simple name (ClassName.Method()), not a variable.
            // Use PascalCase heuristic: type names start with uppercase; variables
            // do not. This is the only signal available without semantic analysis.
            if (memberAccess.Expression is not SimpleNameSyntax targetName) continue;

            var targetClassName = targetName.Identifier.Text;

            // Skip identifiers that look like variables (start with lowercase)
            if (targetClassName.Length == 0 || char.IsLower(targetClassName[0])) continue;

            // Skip own class
            if (targetClassName == ownClass) continue;

            // Skip exception classes
            if (exceptions.Contains(targetClassName)) continue;

            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            ctx.Report(line, line, targetClassName, method.Name);
        }
    }

    private static HashSet<string> SplitList(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return new HashSet<string>(StringComparer.Ordinal);
        return new HashSet<string>(
            val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.Ordinal);
    }
}
