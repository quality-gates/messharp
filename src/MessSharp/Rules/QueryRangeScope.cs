using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules;

internal static class QueryRangeScope
{
    internal static bool IsShadowed(QueryExpressionSyntax query, SyntaxNode usage, string name)
    {
        if (Contains(query.FromClause.Expression, usage)) return false;
        var visible = query.FromClause.Identifier.Text == name;
        var body = query.Body;
        while (true)
        {
            foreach (var clause in body.Clauses)
            {
                if (ClauseVisibility(clause, usage, name, visible) is { } result) return result;
                visible |= Declares(clause, name);
            }
            if (SelectVisibility(body.SelectOrGroup, usage, visible) is { } selected) return selected;
            if (body.Continuation is not { } continuation) return false;
            visible = continuation.Identifier.Text == name;
            body = continuation.Body;
        }
    }

    private static bool? ClauseVisibility(QueryClauseSyntax clause, SyntaxNode usage, string name, bool visible) => clause switch
    {
        FromClauseSyntax from => VisibilityIn(from.Expression, usage, visible),
        LetClauseSyntax let => VisibilityIn(let.Expression, usage, visible),
        WhereClauseSyntax where => VisibilityIn(where.Condition, usage, visible),
        OrderByClauseSyntax order => order.Orderings.Select(o => VisibilityIn(o.Expression, usage, visible)).FirstOrDefault(v => v.HasValue),
        JoinClauseSyntax join => JoinVisibility(join, usage, name, visible),
        _ => null,
    };

    private static bool? JoinVisibility(JoinClauseSyntax join, SyntaxNode usage, string name, bool visible) =>
        VisibilityIn(join.InExpression, usage, false)
        ?? VisibilityIn(join.LeftExpression, usage, visible)
        ?? VisibilityIn(join.RightExpression, usage, join.Identifier.Text == name);

    private static bool Declares(QueryClauseSyntax clause, string name) => clause switch
    {
        FromClauseSyntax from => from.Identifier.Text == name,
        LetClauseSyntax let => let.Identifier.Text == name,
        JoinClauseSyntax { Into: { } into } => into.Identifier.Text == name,
        JoinClauseSyntax join => join.Identifier.Text == name,
        _ => false,
    };

    private static bool? SelectVisibility(SelectOrGroupClauseSyntax clause, SyntaxNode usage, bool visible) => clause switch
    {
        SelectClauseSyntax select => VisibilityIn(select.Expression, usage, visible),
        GroupClauseSyntax group => VisibilityIn(group.GroupExpression, usage, visible) ?? VisibilityIn(group.ByExpression, usage, visible),
        _ => null,
    };

    private static bool? VisibilityIn(ExpressionSyntax expression, SyntaxNode usage, bool visible) =>
        Contains(expression, usage) ? visible : null;

    private static bool Contains(SyntaxNode expression, SyntaxNode usage) =>
        usage.AncestorsAndSelf().Contains(expression);
}
