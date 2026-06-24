using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Design;

/// <summary>
/// Flags plain label-goto statements (goto Label) in methods.
/// goto case / goto default inside switch are idiomatic in C# and are NOT flagged.
/// </summary>
public sealed class GotoStatementRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.Body == null) return;

        foreach (var stmt in method.Body.DescendantNodes().OfType<GotoStatementSyntax>())
        {
            // Only flag plain label goto (Kind == GotoStatement).
            // GotoCaseStatement and GotoDefaultStatement are idiomatic in C# switch.
            if (stmt.IsKind(SyntaxKind.GotoStatement))
            {
                var kind = method.IsConstructor ? "constructor" : "method";
                ctx.ReportMethod(method, kind, method.Name);
                return;
            }
        }
    }
}
