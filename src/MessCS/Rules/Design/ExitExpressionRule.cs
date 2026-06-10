using MessCS.Model;
using MessCS.Rule;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Rules.Design;

/// <summary>
/// Flags calls to Environment.Exit() or Environment.FailFast() within methods.
/// C# analog of phpmd's ExitExpression rule (os.Exit in messgo).
/// </summary>
public sealed class ExitExpressionRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.Body == null) return;

        foreach (var invocation in method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = GetFullName(invocation.Expression);
            if (name == "Environment.Exit" || name == "Environment.FailFast")
            {
                var line = invocation.SyntaxTree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
                var kind = method.IsConstructor ? "constructor" : "method";
                ctx.Report(line, line, kind, method.Name);
                return;
            }
        }
    }

    private static string GetFullName(Microsoft.CodeAnalysis.SyntaxNode expr)
    {
        if (expr is MemberAccessExpressionSyntax ma)
            return ma.ToString();
        if (expr is IdentifierNameSyntax id)
            return id.Identifier.Text;
        return "";
    }
}
