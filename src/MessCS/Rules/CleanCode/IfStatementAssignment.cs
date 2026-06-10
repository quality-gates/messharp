using MessCS.Model;
using MessCS.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Rules.CleanCode;

/// <summary>
/// Flags assignments inside if/while conditions.
/// In C#, assignments in conditions must be wrapped in parentheses like
/// `if ((x = Foo()) != null)`. We detect any AssignmentExpression that
/// appears as a descendant of an if/while condition.
/// Port of phpmd's IfStatementAssignment rule.
/// </summary>
public sealed class IfStatementAssignmentRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.Body == null) return;

        foreach (var node in method.Body.DescendantNodesAndSelf())
        {
            switch (node)
            {
                case IfStatementSyntax ifStmt:
                    CheckCondition(ctx, ifStmt.Condition);
                    break;
                case WhileStatementSyntax whileStmt:
                    CheckCondition(ctx, whileStmt.Condition);
                    break;
            }
        }
    }

    private static void CheckCondition(RuleContext ctx, ExpressionSyntax condition)
    {
        foreach (var assign in condition.DescendantNodesAndSelf()
                     .OfType<AssignmentExpressionSyntax>()
                     .Where(a => a.IsKind(SyntaxKind.SimpleAssignmentExpression)))
        {
            var lineSpan = assign.GetLocation().GetLineSpan();
            int line = lineSpan.StartLinePosition.Line + 1;
            int col = lineSpan.StartLinePosition.Character + 1;
            ctx.Report(line, line, line, col);
        }
    }
}
