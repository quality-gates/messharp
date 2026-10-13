using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules;

internal static class LocalShadowing
{
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
        CatchDeclarationSyntax caught => [caught.Identifier.Text],
        _ => QueryNames(node),
    };

    private static IEnumerable<string> QueryNames(SyntaxNode node) => node switch
    {
        FromClauseSyntax from => [from.Identifier.Text],
        LetClauseSyntax let => [let.Identifier.Text],
        JoinClauseSyntax join => [join.Identifier.Text],
        JoinIntoClauseSyntax into => [into.Identifier.Text],
        QueryContinuationSyntax continuation => [continuation.Identifier.Text],
        _ => [],
    };
}
