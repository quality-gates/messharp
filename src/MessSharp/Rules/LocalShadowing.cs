using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules;

/// <summary>
/// Syntax-only lexical-scope lookup: decides whether a bare identifier at a
/// usage site is bound by a local variable or parameter declared in an
/// enclosing scope, rather than by a member of the containing type.
/// </summary>
internal static class LocalShadowing
{
    /// <summary>
    /// Returns true when <paramref name="name"/> is declared as a local or
    /// parameter in any scope enclosing <paramref name="usage"/>, up to and
    /// including the containing member declaration.
    /// </summary>
    internal static bool IsShadowedByLocal(SyntaxNode usage, string name)
    {
        for (var scope = usage.Parent; scope != null; scope = scope.Parent)
        {
            if (DeclaresName(scope, name)) return true;
            if (scope is MemberDeclarationSyntax) break;
        }
        return false;
    }

    private static bool DeclaresName(SyntaxNode scope, string name) =>
        ParameterNames(scope).Contains(name) || LocalNames(scope).Contains(name);

    /// <summary>
    /// Names of the parameters a scope introduces (methods, local functions,
    /// lambdas and anonymous methods).
    /// </summary>
    private static IEnumerable<string> ParameterNames(SyntaxNode scope) => scope switch
    {
        BaseMethodDeclarationSyntax m => Names(m.ParameterList),
        LocalFunctionStatementSyntax f => Names(f.ParameterList),
        ParenthesizedLambdaExpressionSyntax l => Names(l.ParameterList),
        AnonymousMethodExpressionSyntax a => Names(a.ParameterList),
        SimpleLambdaExpressionSyntax s => [s.Parameter.Identifier.Text],
        _ => [],
    };

    private static IEnumerable<string> Names(BaseParameterListSyntax? parameters) =>
        parameters?.Parameters.Select(p => p.Identifier.Text) ?? [];

    /// <summary>
    /// Names of the locals a scope introduces directly, without descending
    /// into nested scopes that own their own declarations.
    /// </summary>
    private static IEnumerable<string> LocalNames(SyntaxNode scope) =>
        scope.DescendantNodes(descendIntoChildren: n => ReferenceEquals(n, scope) || !IsScopeBoundary(n))
             .SelectMany(DeclaredNames);

    private static bool IsScopeBoundary(SyntaxNode node) =>
        node is BlockSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax;

    private static IEnumerable<string> DeclaredNames(SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax v => [v.Identifier.Text],
        SingleVariableDesignationSyntax d => [d.Identifier.Text],
        ForEachStatementSyntax f => [f.Identifier.Text],
        _ => [],
    };
}
