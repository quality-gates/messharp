using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>Every assignment and argument in a file, listed once instead of rescanned per deferred read.</summary>
internal static class FileWriteSites
{
    private static readonly ConditionalWeakTable<SyntaxNode, IReadOnlyList<SyntaxNode>> Sites = new();

    public static IReadOnlyList<SyntaxNode> For(SyntaxNode root) => Sites.GetValue(root, node =>
        node.DescendantNodes().Where(candidate => candidate is AssignmentExpressionSyntax or ArgumentSyntax).ToList());
}
