using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal readonly record struct SequenceConsumption(ExpressionSyntax Source, SyntaxNode Site)
{
    public static SequenceConsumption? Find(CallableOwner owner, SyntaxNode node) => node switch
    {
        CommonForEachStatementSyntax loop => new(loop.Expression, loop),
        SpreadElementSyntax spread => new(spread.Expression, spread),
        BaseObjectCreationExpressionSyntax creation when KnownExecutionOperations.ListSequenceArgument(owner, creation) is { } source =>
            new(source, creation),
        InvocationExpressionSyntax call when KnownExecutionOperations.AddRangeSequenceArgument(owner, call) is { } source =>
            new(source, call),
        InvocationExpressionSyntax call when KnownExecutionOperations.IsSequenceForce(owner, call) =>
            new(((MemberAccessExpressionSyntax)call.Expression).Expression, call),
        _ => null,
    };
}
