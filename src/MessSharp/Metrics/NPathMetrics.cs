using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Metrics;

/// <summary>
/// NPath complexity using Nejmeh's algorithm, as implemented by pdepend's
/// NPathComplexityAnalyzer. Values pinned to phpmd 2.15.0 reference output.
/// </summary>
internal static class NPathMetrics
{
    internal static int Compute(BlockSyntax? body)
    {
        if (body == null) return 1;
        return Stmts(body.Statements);
    }

    private static int Stmts(SyntaxList<StatementSyntax> stmts)
    {
        int product = 1;
        foreach (var s in stmts)
            product *= Stmt(s);
        return product;
    }

    private static int Stmt(StatementSyntax s) => s switch
    {
        IfStatementSyntax ifStmt => NPathIf(ifStmt),
        ForStatementSyntax forStmt => NPathFor(forStmt),
        ForEachStatementSyntax fe => ExprComplexity(fe.Expression) + 1 + Block(fe.Statement),
        WhileStatementSyntax ws => ExprComplexity(ws.Condition) + 1 + Block(ws.Statement),
        DoStatementSyntax ds => ExprComplexity(ds.Condition) + 1 + Block(ds.Statement),
        SwitchStatementSyntax sw => NPathSwitch(sw),
        BlockSyntax blk => Stmts(blk.Statements),
        ReturnStatementSyntax ret => ReturnComplexity(ret),
        LabeledStatementSyntax lbl => Stmt(lbl.Statement),
        _ => 1,
    };

    /// <summary>NP(if) = NP(else-part) + NP(if-body) + Σ expr</summary>
    private static int NPathIf(IfStatementSyntax n)
    {
        int expr = ExprComplexity(n.Condition);
        int body = Block(n.Statement);
        int elsePart = n.Else switch
        {
            null => 1,
            { Statement: IfStatementSyntax elseIf } => NPathIf(elseIf),
            { Statement: BlockSyntax blk } => Stmts(blk.Statements),
            { Statement: var other } => Stmt(other),
        };
        return elsePart + body + expr;
    }

    private static int NPathFor(ForStatementSyntax n)
    {
        int npath = 1 + ExprComplexity(n.Condition);
        foreach (var init in n.Initializers)
            npath += ExprComplexity(init);
        foreach (var incr in n.Incrementors)
            npath += ExprComplexity(incr);
        npath += Block(n.Statement);
        return npath;
    }

    private static int NPathSwitch(SwitchStatementSyntax sw)
    {
        int npath = ExprComplexity(sw.Expression);
        foreach (var section in sw.Sections)
            npath += Stmts(section.Statements);
        return npath == 0 ? 1 : npath;
    }

    private static int Block(StatementSyntax stmt) => stmt switch
    {
        BlockSyntax blk => Stmts(blk.Statements),
        _ => Stmt(stmt),
    };

    private static int ReturnComplexity(ReturnStatementSyntax ret)
    {
        if (ret.Expression == null) return 1;
        int c = ExprComplexity(ret.Expression);
        return c == 0 ? 1 : c;
    }

    /// <summary>
    /// Counts boolean operators (&amp;&amp;, ||) and null-coalescing (??) in an
    /// expression, matching pdepend's expressionComplexity.
    /// </summary>
    private static int ExprComplexity(ExpressionSyntax? expr)
    {
        if (expr == null) return 0;
        int count = 0;
        foreach (var node in expr.DescendantNodesAndSelf())
        {
            if (node is BinaryExpressionSyntax bin && IsBooleanOp(bin))
                count++;
        }
        return count;
    }

    private static int ExprComplexity(SyntaxNode? expr)
    {
        if (expr == null) return 0;
        return expr is ExpressionSyntax e ? ExprComplexity(e) : 0;
    }

    private static bool IsBooleanOp(BinaryExpressionSyntax bin) =>
        bin.IsKind(SyntaxKind.LogicalAndExpression)
        || bin.IsKind(SyntaxKind.LogicalOrExpression)
        || bin.IsKind(SyntaxKind.CoalesceExpression);
}
