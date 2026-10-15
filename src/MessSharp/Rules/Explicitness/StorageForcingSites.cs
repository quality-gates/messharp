using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class StorageForcingSites
{
    public static ExpressionSyntax? Sequence(SyntaxNode node) => node switch
    {
        ForEachStatementSyntax loop => loop.Expression,
        ForEachVariableStatementSyntax loop => loop.Expression,
        _ => null,
    };

    public static bool MayStopEarly(SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } => member.Name.Identifier.Text is "Any" or "All",
        ForEachStatementSyntax loop => Stops(loop.Statement),
        ForEachVariableStatementSyntax loop => Stops(loop.Statement),
        _ => false,
    };

    private static bool Stops(SyntaxNode body) => BodyWalk.Nodes(body).Any(child => child is BreakStatementSyntax or ReturnStatementSyntax);
}
