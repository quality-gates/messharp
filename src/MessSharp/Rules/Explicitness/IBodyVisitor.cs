using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Looks at each node of a method body in one shared walk, in document order
/// with each node before its children.
/// </summary>
internal interface IBodyVisitor
{
    void Visit(SyntaxNode node);
}

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
