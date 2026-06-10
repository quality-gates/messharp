using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Metrics;

/// <summary>
/// Code metrics computed from Roslyn syntax: cyclomatic complexity, NPath
/// complexity, and lines of code. Mirrors messgo's metrics package, adapted
/// to C# AST nodes, with values pinned to phpmd 2.15.0 reference output.
/// </summary>
public static class Metrics
{
    /// <summary>
    /// Cyclomatic complexity: base 1 + 1 per decision point.
    /// Decision points: if, case label (not default), for, foreach, while,
    /// do, catch, &&, ||, ??, ternary ?:.
    /// </summary>
    public static int CyclomaticComplexity(BlockSyntax? body)
    {
        if (body == null) return 1;
        int ccn = 1;
        foreach (var node in body.DescendantNodesAndSelf())
            ccn += CcnIncrement(node);
        return ccn;
    }

    private static int CcnIncrement(SyntaxNode node)
    {
        switch (node)
        {
            case IfStatementSyntax:
            case ForStatementSyntax:
            case ForEachStatementSyntax:
            case WhileStatementSyntax:
            case DoStatementSyntax:
            case CatchClauseSyntax:
                return 1;
            case SwitchSectionSyntax section:
                // count non-default labels
                return section.Labels.Count(l => l is CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax);
            case BinaryExpressionSyntax bin
                when bin.IsKind(SyntaxKind.LogicalAndExpression)
                  || bin.IsKind(SyntaxKind.LogicalOrExpression)
                  || bin.IsKind(SyntaxKind.CoalesceExpression):
                return 1;
            case ConditionalExpressionSyntax:
                return 1;
            default:
                return 0;
        }
    }

    /// <summary>
    /// NPath complexity using Nejmeh's algorithm, as implemented by
    /// pdepend's NPathComplexityAnalyzer.
    /// </summary>
    public static int NPathComplexity(BlockSyntax? body)
    {
        if (body == null) return 1;
        return NPathStmts(body.Statements);
    }

    private static int NPathStmts(SyntaxList<StatementSyntax> stmts)
    {
        int product = 1;
        foreach (var s in stmts)
            product *= NPathStmt(s);
        return product;
    }

    private static int NPathStmt(StatementSyntax s)
    {
        return s switch
        {
            IfStatementSyntax ifStmt => NPathIf(ifStmt),
            ForStatementSyntax forStmt => NPathFor(forStmt),
            ForEachStatementSyntax fe => ExprComplexity(fe.Expression) + 1 + NPathBlock(fe.Statement),
            WhileStatementSyntax ws => ExprComplexity(ws.Condition) + 1 + NPathBlock(ws.Statement),
            DoStatementSyntax ds => ExprComplexity(ds.Condition) + 1 + NPathBlock(ds.Statement),
            SwitchStatementSyntax sw => NPathSwitch(sw),
            BlockSyntax blk => NPathStmts(blk.Statements),
            ReturnStatementSyntax ret => ReturnComplexity(ret),
            LabeledStatementSyntax lbl => NPathStmt(lbl.Statement),
            _ => 1,
        };
    }

    /// <summary>
    /// NP(if) = NP(else-part) + NP(if-body) + Σ expr
    /// </summary>
    private static int NPathIf(IfStatementSyntax n)
    {
        int expr = ExprComplexity(n.Condition);
        int body = NPathBlock(n.Statement);
        int elsePart = n.Else switch
        {
            null => 1,
            { Statement: IfStatementSyntax elseIf } => NPathIf(elseIf),
            { Statement: BlockSyntax blk } => NPathStmts(blk.Statements),
            { Statement: var other } => NPathStmt(other),
        };
        return elsePart + body + expr;
    }

    private static int NPathFor(ForStatementSyntax n)
    {
        int npath = 1;
        npath += ExprComplexity(n.Condition);
        foreach (var init in n.Initializers)
            npath += ExprComplexity(init);
        foreach (var incr in n.Incrementors)
            npath += ExprComplexity(incr);
        npath += NPathBlock(n.Statement);
        return npath;
    }

    private static int NPathSwitch(SwitchStatementSyntax sw)
    {
        int npath = ExprComplexity(sw.Expression);
        foreach (var section in sw.Sections)
            npath += NPathStmts(section.Statements);
        return npath == 0 ? 1 : npath;
    }

    private static int NPathBlock(StatementSyntax stmt)
    {
        return stmt switch
        {
            BlockSyntax blk => NPathStmts(blk.Statements),
            _ => NPathStmt(stmt),
        };
    }

    private static int ReturnComplexity(ReturnStatementSyntax ret)
    {
        if (ret.Expression == null) return 1;
        int c = ExprComplexity(ret.Expression);
        return c == 0 ? 1 : c;
    }

    /// <summary>
    /// Counts boolean operators (&&, ||) and null-coalescing (??) in an
    /// expression, matching pdepend's expressionComplexity.
    /// </summary>
    private static int ExprComplexity(ExpressionSyntax? expr)
    {
        if (expr == null) return 0;
        int count = 0;
        foreach (var node in expr.DescendantNodesAndSelf())
        {
            if (node is BinaryExpressionSyntax bin
                && (bin.IsKind(SyntaxKind.LogicalAndExpression)
                 || bin.IsKind(SyntaxKind.LogicalOrExpression)
                 || bin.IsKind(SyntaxKind.CoalesceExpression)))
                count++;
        }
        return count;
    }

    /// <summary>Counts boolean operators in a general expression node.</summary>
    private static int ExprComplexity(SyntaxNode? expr)
    {
        if (expr == null) return 0;
        return expr is ExpressionSyntax e ? ExprComplexity(e) : 0;
    }

    /// <summary>
    /// Lines of code spanned by a node (inclusive, 1-based).
    /// </summary>
    public static int LinesOfCode(SyntaxNode node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    /// <summary>
    /// Effective LOC: lines that contain code, skipping blank lines and
    /// comment-only lines.
    /// </summary>
    public static int EffectiveLinesOfCode(SyntaxNode node, string source)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        int first = span.StartLinePosition.Line;
        int last = span.EndLinePosition.Line;
        var lines = source.Split('\n');
        int count = 0;
        bool inBlock = false;
        for (int i = 0; i < lines.Length; i++)
        {
            bool hasCode;
            (hasCode, inBlock) = LineHasCode(lines[i], inBlock);
            if (i >= first && i <= last && hasCode)
                count++;
        }
        return count;
    }

    private static (bool hasCode, bool blockAfter) LineHasCode(string line, bool inBlock)
    {
        bool hasCode = false;
        int i = 0;
        while (i < line.Length)
        {
            char ch = line[i];
            if (inBlock)
            {
                if (ch == '*' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    inBlock = false;
                    i += 2;
                }
                else i++;
                continue;
            }
            if (ch == '/' && i + 1 < line.Length)
            {
                if (line[i + 1] == '/') return (hasCode, false);
                if (line[i + 1] == '*') { inBlock = true; i += 2; continue; }
            }
            if (ch != ' ' && ch != '\t' && ch != '\r')
                hasCode = true;
            i++;
        }
        return (hasCode, inBlock);
    }
}
