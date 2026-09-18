using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// Collects the specific IdentifierNameSyntax nodes that appear only as
/// assignment or declaration targets (pure writes), for use by
/// <see cref="BodyAnalysis.IdentReads"/>. Split out from BodyAnalysis to
/// keep that class's overall complexity within its configured threshold.
/// </summary>
internal static class WriteIdentCollector
{
    internal static HashSet<SyntaxNode> Collect(SyntaxNode body)
    {
        var writes = new HashSet<SyntaxNode>(ReferenceEqualityComparer.Instance);

        foreach (var node in body.DescendantNodesAndSelf())
            CollectFromNode(node, writes);

        return writes;
    }

    private static void CollectFromNode(SyntaxNode node, HashSet<SyntaxNode> writes)
    {
        switch (node)
        {
            // simple assignment LHS is a pure write; compound assignments
            // read the existing left-hand value before writing it back.
            case AssignmentExpressionSyntax aes
                when aes.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    && aes.Left is IdentifierNameSyntax lhsId:
                writes.Add(lhsId);
                break;

            // tuple deconstruction assignment: (x, y) = (1, 2);
            // each identifier target inside the LHS tuple is a pure write,
            // same as a simple assignment target.
            case AssignmentExpressionSyntax aes
                when aes.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    && aes.Left is TupleExpressionSyntax tuple:
                CollectTupleWriteIdents(tuple, writes);
                break;
        }
    }

    /// <summary>
    /// Recursively marks identifier targets inside a tuple deconstruction
    /// assignment's LHS (e.g. `(x, (y, _)) = ...`) as pure writes. Nested
    /// `var` declarations introduce new locals, not writes to existing ones,
    /// so they are skipped.
    /// </summary>
    private static void CollectTupleWriteIdents(TupleExpressionSyntax tuple, HashSet<SyntaxNode> writes)
    {
        foreach (var arg in tuple.Arguments)
        {
            switch (arg.Expression)
            {
                case IdentifierNameSyntax id:
                    writes.Add(id);
                    break;
                case TupleExpressionSyntax nested:
                    CollectTupleWriteIdents(nested, writes);
                    break;
            }
        }
    }
}
