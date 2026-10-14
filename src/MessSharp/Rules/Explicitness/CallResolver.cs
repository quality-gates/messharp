using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The declared callable an invocation names: a local function, a method or a member. A call through a delegate
/// value resolves elsewhere, from the callables the value can hold at the call.
/// </summary>
internal sealed class CallResolver(IReadOnlyList<CallableOwner> owners)
{
    private readonly CallableLookup _lookup = new(owners);

    public CallableOwner? Resolve(CallableOwner caller, InvocationExpressionSyntax call) => call.Expression switch
    {
        IdentifierNameSyntax bare => ResolveBare(caller, bare, CallShape.From(call.ArgumentList)),
        MemberAccessExpressionSyntax member => _lookup.MemberTarget(caller, member, CallShape.From(call.ArgumentList)),
        _ => null,
    };

    private CallableOwner? ResolveBare(CallableOwner caller, IdentifierNameSyntax name, CallShape shape)
    {
        if (_lookup.VisibleLocal(caller, name, shape) is { } local) return local;
        return LocalShadowing.IsShadowedByLocal(name, name.Identifier.Text) ? null : _lookup.SameClassMethod(caller, name, shape);
    }
}

internal static class CallableSyntax
{
    public static IEnumerable<ExpressionSyntax> Returns(SyntaxNode? body) => body switch
    {
        null => [],
        ExpressionSyntax expression => [expression],
        _ => BodyWalk.Nodes(body).OfType<ReturnStatementSyntax>().Select(statement => statement.Expression).OfType<ExpressionSyntax>(),
    };

    public static ExpressionSyntax Unwrap(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax grouped => Unwrap(grouped.Expression),
        CastExpressionSyntax cast => Unwrap(cast.Expression),
        _ => expression,
    };
}
