using MessCS.Model;
using MessCS.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Rules.Design;

/// <summary>
/// Flags .Count(), .Count, or .Length accesses inside loop conditions (for/while/do).
/// C# analog of phpmd's CountInLoopExpression (len/cap in Go loops).
/// </summary>
public sealed class CountInLoopExpressionRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.Body == null) return;

        foreach (var node in method.Body.DescendantNodes())
        {
            switch (node)
            {
                case ForStatementSyntax forStmt when forStmt.Condition != null:
                    CheckCondition(ctx, forStmt.Condition, "for");
                    break;

                case WhileStatementSyntax whileStmt:
                    CheckCondition(ctx, whileStmt.Condition, "while");
                    break;

                case DoStatementSyntax doStmt:
                    CheckCondition(ctx, doStmt.Condition, "do");
                    break;
            }
        }
    }

    private static void CheckCondition(RuleContext ctx, ExpressionSyntax condition, string loopKind)
    {
        foreach (var access in condition.DescendantNodesAndSelf())
        {
            string? countName = GetCountName(access);
            if (countName != null)
            {
                var line = condition.SyntaxTree.GetLineSpan(condition.Span).StartLinePosition.Line + 1;
                ctx.Report(line, line, countName, loopKind);
                return;
            }
        }
    }

    private static string? GetCountName(SyntaxNode node)
    {
        // .Count() — LINQ extension method call
        if (node is InvocationExpressionSyntax inv &&
            inv.Expression is MemberAccessExpressionSyntax maInv &&
            maInv.Name.Identifier.Text == "Count")
            return "Count";

        // .Count or .Length — property access
        if (node is MemberAccessExpressionSyntax ma)
        {
            var name = ma.Name.Identifier.Text;
            if (name == "Count" || name == "Length")
                return name;
        }

        return null;
    }
}
