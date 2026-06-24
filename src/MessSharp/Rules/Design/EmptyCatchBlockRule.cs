using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Design;

/// <summary>
/// Flags catch blocks that are empty or contain only comments.
/// A lone comment still counts as empty, matching phpmd behavior.
/// </summary>
public sealed class EmptyCatchBlockRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.Body == null) return;

        foreach (var tryCatch in method.Body.DescendantNodes().OfType<TryStatementSyntax>())
        {
            foreach (var catchClause in tryCatch.Catches)
            {
                if (IsEmptyOrCommentOnly(catchClause.Block))
                {
                    var line = catchClause.SyntaxTree
                        .GetLineSpan(catchClause.Span).StartLinePosition.Line + 1;
                    ctx.Report(line, line, method.Name);
                    return;
                }
            }
        }
    }

    private static bool IsEmptyOrCommentOnly(BlockSyntax block)
    {
        // No statements = empty
        if (block.Statements.Count == 0)
            return true;

        // All tokens that are not braces are trivia (comments)
        // If there are statements we have real code
        return false;
    }
}
