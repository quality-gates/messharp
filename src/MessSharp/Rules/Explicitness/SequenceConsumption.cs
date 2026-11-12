using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal readonly record struct SequenceConsumption(ExpressionSyntax Source, SyntaxNode Site, bool Ambiguous = false)
{
    public static SequenceConsumption? Find(CallableOwner owner, SyntaxNode node, CallResolver calls) => node switch
    {
        CommonForEachStatementSyntax loop => new(loop.Expression, loop),
        SpreadElementSyntax spread => new(spread.Expression, spread),
        BaseObjectCreationExpressionSyntax creation when KnownListConsumption.ConstructorArgument(owner, creation) is { } source =>
            new(source, creation),
        InvocationExpressionSyntax call when KnownEnumerableContains.Source(owner, call) is { } source =>
            new(source, call),
        InvocationExpressionSyntax call when KnownListConsumption.AddRangeArgument(owner, call) is { } source =>
            new(source, call),
        InvocationExpressionSyntax call when KnownCollectionContains.Receiver(owner, call) is { } receiver =>
            new(receiver, call),
        InvocationExpressionSyntax call when KnownExecutionOperations.IsSequenceForce(owner, call)
            && calls.Resolve(owner, call) is null =>
            new(((MemberAccessExpressionSyntax)call.Expression).Expression, call, calls.HasPossibleExtension(call)),
        _ => null,
    };
}
