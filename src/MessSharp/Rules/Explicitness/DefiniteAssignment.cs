using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Whether a statement always replaces a declaration's value before control leaves it normally: a plain
/// assignment, both arms of an if/else, the live arm of a constant condition, a block, or a loop body that
/// runs at least once and cannot be left early.
/// </summary>
internal sealed class DefiniteAssignment(Func<SyntaxNode, bool> writes)
{
    /// <summary>The latest statement before <paramref name="reference"/>, in a block enclosing it, that always assigns.</summary>
    public SyntaxNode? LatestBefore(SyntaxNode body, SyntaxNode reference) =>
        reference.Ancestors().Where(node => body.Span.Contains(node.Span)).OfType<BlockSyntax>()
            .SelectMany(block => block.Statements.Where(s => s.Span.End <= reference.SpanStart && Assigns(s)))
            .MaxBy(statement => statement.SpanStart);

    public bool Assigns(SyntaxNode statement) => statement switch
    {
        ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } =>
            assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) && writes(assignment),
        BlockSyntax block => Sequence(block.Statements),
        IfStatementSyntax choice => Branches(choice),
        DoStatementSyntax loop => Assigns(loop.Statement),
        WhileStatementSyntax { Condition: var condition } loop when LiteralControlFlow.Boolean(condition) == true =>
            Assigns(loop.Statement),
        _ => false,
    };

    private bool Branches(IfStatementSyntax choice) => LiteralControlFlow.Boolean(choice.Condition) switch
    {
        true => Assigns(choice.Statement),
        false => choice.Else is { } other && Assigns(other.Statement),
        _ => choice.Else is { } other && Assigns(choice.Statement) && Assigns(other.Statement),
    };

    private bool Sequence(SyntaxList<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            if (Assigns(statement)) return true;
            if (Escapes(statement)) return false;
        }
        return false;
    }

    /// <summary>Whether a break, continue or goto in the statement can skip what follows it.</summary>
    private static bool Escapes(SyntaxNode statement) => statement.DescendantNodesAndSelf(node => node is not AnonymousFunctionExpressionSyntax
        and not LocalFunctionStatementSyntax).Any(node => node is BreakStatementSyntax or ContinueStatementSyntax or GotoStatementSyntax);
}
