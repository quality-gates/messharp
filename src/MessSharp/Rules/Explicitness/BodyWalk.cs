using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

internal static class BodyWalk
{
    /// <summary>Walks the body once, showing each node to every visitor.</summary>
    public static void Run(SyntaxNode body, IReadOnlyList<IBodyVisitor> visitors)
    {
        foreach (var node in body.DescendantNodesAndSelf())
        {
            foreach (var visitor in visitors)
                visitor.Visit(node);
        }
    }
}
