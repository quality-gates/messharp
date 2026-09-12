using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Metrics;

/// <summary>
/// NPath complexity for statement bodies and their control-flow constructs.
/// </summary>
internal static class NPathStatementMetrics
{
    internal static int Compute(SyntaxList<StatementSyntax> statements) => Stmts(statements);

    private static int Stmts(SyntaxList<StatementSyntax> statements)
    {
        int product = 1;
        foreach (var statement in statements)
            product = NPathArithmetic.Multiply(product, Stmt(statement));
        return product;
    }

    private static int Stmt(StatementSyntax statement) => statement switch
    {
        IfStatementSyntax ifStatement => NPathIf(ifStatement),
        ForStatementSyntax forStatement => NPathFor(forStatement),
        ForEachStatementSyntax forEach => NPathArithmetic.Add(
            NPathArithmetic.Add(NPathExpressionMetrics.Complexity(forEach.Expression), 1),
            Block(forEach.Statement)),
        WhileStatementSyntax whileStatement => NPathArithmetic.Add(
            NPathArithmetic.Add(NPathExpressionMetrics.Complexity(whileStatement.Condition), 1),
            Block(whileStatement.Statement)),
        DoStatementSyntax doStatement => NPathArithmetic.Add(
            NPathArithmetic.Add(NPathExpressionMetrics.Complexity(doStatement.Condition), 1),
            Block(doStatement.Statement)),
        _ => StmtStructural(statement),
    };

    private static int StmtStructural(StatementSyntax statement) => statement switch
    {
        TryStatementSyntax tryStatement => NPathTry(tryStatement),
        SwitchStatementSyntax switchStatement => NPathSwitch(switchStatement),
        BlockSyntax block => Stmts(block.Statements),
        ReturnStatementSyntax returnStatement => ExpressionContribution(returnStatement.Expression),
        LocalDeclarationStatementSyntax local => LocalDeclarationComplexity(local),
        ExpressionStatementSyntax expressionStatement => ExpressionContribution(expressionStatement.Expression),
        LabeledStatementSyntax labeled => Stmt(labeled.Statement),
        _ => StmtScoped(statement),
    };

    /// <summary>
    /// Scope wrappers (using/lock/fixed/unsafe) introduce no branch of their own,
    /// so they compose like a sequence: the resource they acquire followed by the
    /// body they wrap. Without this the wrapped body would be discarded entirely.
    /// </summary>
    private static int StmtScoped(StatementSyntax statement) => statement switch
    {
        UsingStatementSyntax usingStatement => NPathArithmetic.Multiply(
            ResourceComplexity(usingStatement.Declaration, usingStatement.Expression),
            Block(usingStatement.Statement)),
        LockStatementSyntax lockStatement => NPathArithmetic.Multiply(
            ExpressionContribution(lockStatement.Expression),
            Block(lockStatement.Statement)),
        FixedStatementSyntax fixedStatement => NPathArithmetic.Multiply(
            DeclarationComplexity(fixedStatement.Declaration),
            Block(fixedStatement.Statement)),
        UnsafeStatementSyntax unsafeStatement => Stmts(unsafeStatement.Block.Statements),
        _ => 1,
    };

    /// <summary>NP(if) = NP(else-part) + NP(if-body) + Σ expr</summary>
    private static int NPathIf(IfStatementSyntax statement)
    {
        int expression = NPathExpressionMetrics.Complexity(statement.Condition);
        int body = Block(statement.Statement);
        int elsePart = statement.Else switch
        {
            null => 1,
            { Statement: IfStatementSyntax elseIf } => NPathIf(elseIf),
            { Statement: BlockSyntax block } => Stmts(block.Statements),
            { Statement: var other } => Stmt(other),
        };
        return NPathArithmetic.Add(
            NPathArithmetic.Add(elsePart, body),
            expression);
    }

    private static int NPathFor(ForStatementSyntax statement)
    {
        int npath = NPathArithmetic.Add(
            1,
            NPathExpressionMetrics.Complexity(statement.Condition));
        foreach (var initializer in statement.Initializers)
            npath = NPathArithmetic.Add(
                npath,
                NPathExpressionMetrics.Complexity(initializer));
        foreach (var incrementor in statement.Incrementors)
            npath = NPathArithmetic.Add(
                npath,
                NPathExpressionMetrics.Complexity(incrementor));
        return NPathArithmetic.Add(npath, Block(statement.Statement));
    }

    private static int NPathTry(TryStatementSyntax statement)
    {
        int npath = Stmts(statement.Block.Statements);
        foreach (var catchClause in statement.Catches)
            npath = NPathArithmetic.Add(npath, Stmts(catchClause.Block.Statements));
        if (statement.Finally != null)
            npath = NPathArithmetic.Add(npath, Stmts(statement.Finally.Block.Statements));
        return npath == 0 ? 1 : npath;
    }

    private static int NPathSwitch(SwitchStatementSyntax statement)
    {
        int npath = NPathExpressionMetrics.Complexity(statement.Expression);
        foreach (var section in statement.Sections)
            npath = NPathArithmetic.Add(npath, Stmts(section.Statements));
        return npath == 0 ? 1 : npath;
    }

    private static int Block(StatementSyntax statement) => statement switch
    {
        BlockSyntax block => Stmts(block.Statements),
        _ => Stmt(statement),
    };

    private static int LocalDeclarationComplexity(LocalDeclarationStatementSyntax statement) =>
        DeclarationComplexity(statement.Declaration);

    /// <summary>A using statement acquires either a declaration or a plain expression.</summary>
    private static int ResourceComplexity(
        VariableDeclarationSyntax? declaration,
        ExpressionSyntax? expression) =>
        declaration == null
            ? ExpressionContribution(expression)
            : DeclarationComplexity(declaration);

    private static int DeclarationComplexity(VariableDeclarationSyntax declaration)
    {
        int complexity = 0;
        foreach (var variable in declaration.Variables)
            complexity = NPathArithmetic.Add(
                complexity,
                NPathExpressionMetrics.Complexity(variable.Initializer?.Value));
        return complexity == 0 ? 1 : complexity;
    }

    private static int ExpressionContribution(ExpressionSyntax? expression)
    {
        if (expression == null) return 1;
        int complexity = NPathExpressionMetrics.Complexity(expression);
        return complexity == 0 ? 1 : complexity;
    }
}
