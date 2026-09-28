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
