using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The delegate types a file declares, collected once instead of rescanned per lazy factory.</summary>
internal static class FileDelegateNames
{
    private static readonly ConditionalWeakTable<SyntaxNode, IReadOnlySet<string>> Names = new();

    public static bool Declares(SyntaxNode root, string name) => Names.GetValue(root, node =>
        node.DescendantNodes().OfType<DelegateDeclarationSyntax>().Select(declared => declared.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal)).Contains(name);
}
