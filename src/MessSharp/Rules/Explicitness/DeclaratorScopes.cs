using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The declarators each block or compilation unit owns directly, by name and in source order.</summary>
internal static class DeclaratorScopes
{
    private static readonly ConditionalWeakTable<SyntaxNode, ILookup<string, VariableDeclaratorSyntax>> Scopes = new();

    public static IEnumerable<VariableDeclaratorSyntax> Named(SyntaxNode scope, string name) =>
        Scopes.GetValue(scope, Index)[name];

    private static ILookup<string, VariableDeclaratorSyntax> Index(SyntaxNode scope) => scope.DescendantNodes()
        .OfType<VariableDeclaratorSyntax>()
        .Where(v => v.Ancestors().FirstOrDefault(n => n is BlockSyntax or CompilationUnitSyntax) == scope)
        .ToLookup(v => v.Identifier.Text, StringComparer.Ordinal);
}
