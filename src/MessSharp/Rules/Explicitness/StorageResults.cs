using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class StorageResults
{
    public static StorageOrigin Collect(SyntaxNode body, StorageFlow flow, StorageEnvironment initial, StorageEnvironment final, bool weak)
    {
        var returned = StorageOrigin.Private;
        foreach (var expression in Expressions(body))
        {
            var atReturn = flow.Before(expression, initial);
            returned = returned.Join(flow.Origin(expression, atReturn));
            if (weak || expression.Parent is not YieldStatementSyntax) final.Heap.Join(atReturn.Heap);
        }
        return returned;
    }

    private static IEnumerable<ExpressionSyntax> Expressions(SyntaxNode body) => body is ExpressionSyntax expression
        ? [expression] : BodyWalk.Nodes(body).Select(node => node switch
        {
            ReturnStatementSyntax returned => returned.Expression,
            YieldStatementSyntax yielded => yielded.Expression,
            _ => null,
        }).OfType<ExpressionSyntax>();
}
