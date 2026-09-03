using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Design;

/// <summary>
/// Flags calls to Environment.Exit() or Environment.FailFast() within methods.
/// C# analog of phpmd's ExitExpression rule (os.Exit in messgo).
/// </summary>
public sealed class ExitExpressionRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        var body = method.EffectiveBody;
        if (body == null) return;

        foreach (var invocation in body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            var name = GetFullName(invocation.Expression);
            if (IsExitCall(name))
            {
                var line = invocation.SyntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
                var kind = method.IsConstructor ? "constructor" : "method";
                ctx.Report(line, line, kind, method.Name);
                return;
            }
        }
    }

    private static bool IsExitCall(string name) =>
        name == "Environment.Exit" || name.EndsWith(".Environment.Exit", StringComparison.Ordinal) ||
        name == "Environment.FailFast" || name.EndsWith(".Environment.FailFast", StringComparison.Ordinal);

    private static string GetFullName(Microsoft.CodeAnalysis.SyntaxNode expr)
    {
        if (expr is MemberAccessExpressionSyntax ma)
            return ma.ToString();
        if (expr is IdentifierNameSyntax id)
            return id.Identifier.Text;
        return "";
    }
}
