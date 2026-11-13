using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules;

/// <summary>
/// The parameter and local names one lexical scope node declares on its own,
/// not those of scopes nested inside it. Computed once per node and cached,
/// because every identifier lookup walks the same enclosing scopes.
/// </summary>
internal static class ScopeDeclarations
{
    private static readonly ConditionalWeakTable<SyntaxNode, HashSet<string>> Cache = new();

    internal static bool Declares(SyntaxNode scope, string name) => Cache.GetValue(scope, Names).Contains(name);

    private static HashSet<string> Names(SyntaxNode scope) =>
        new(ParameterNames(scope).Concat(LocalNames(scope)), StringComparer.Ordinal);

    private static IEnumerable<string> ParameterNames(SyntaxNode scope) => scope switch
    {
        BaseMethodDeclarationSyntax m => Names(m.ParameterList),
        IndexerDeclarationSyntax i => Names(i.ParameterList),
        LocalFunctionStatementSyntax f => Names(f.ParameterList),
        ParenthesizedLambdaExpressionSyntax l => Names(l.ParameterList),
        AnonymousMethodExpressionSyntax a => Names(a.ParameterList),
        SimpleLambdaExpressionSyntax s => [s.Parameter.Identifier.Text],
        ForEachStatementSyntax f => [f.Identifier.Text],
        _ => [],
    };

    private static IEnumerable<string> Names(BaseParameterListSyntax? parameters) =>
        parameters?.Parameters.Select(p => p.Identifier.Text) ?? [];

    private static IEnumerable<string> LocalNames(SyntaxNode scope) =>
        scope.DescendantNodes(descendIntoChildren: n => ReferenceEquals(n, scope) || !IsScopeBoundary(n))
             .SelectMany(DeclaredNames);

    internal static bool IsScopeBoundary(SyntaxNode node) =>
        node is BlockSyntax or CatchClauseSyntax or CommonForEachStatementSyntax
            or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax
        || QueryRangeScope.IsBoundary(node);

    private static IEnumerable<string> DeclaredNames(SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax v => [v.Identifier.Text],
        SingleVariableDesignationSyntax d => [d.Identifier.Text],
        CatchDeclarationSyntax caught => [caught.Identifier.Text],
        _ => [],
    };
}
