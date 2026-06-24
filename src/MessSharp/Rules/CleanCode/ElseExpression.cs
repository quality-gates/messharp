using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.CleanCode;

/// <summary>
/// Flags any else block that is not an else-if chain.
/// An if expression with an else branch is basically not necessary.
/// Port of phpmd's ElseExpression rule.
/// </summary>
public sealed class ElseExpressionRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.Body == null) return;

        foreach (var ifStmt in method.Body.DescendantNodesAndSelf()
                     .OfType<IfStatementSyntax>())
        {
            if (ifStmt.Else == null) continue;

            // An else-if: the else clause's statement is another IfStatementSyntax
            if (ifStmt.Else.Statement is IfStatementSyntax) continue;

            var line = ifStmt.Else.GetLocation()
                .GetLineSpan().StartLinePosition.Line + 1;
            ctx.Report(line, line, method.Name);
        }
    }
}
