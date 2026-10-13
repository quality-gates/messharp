using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class CallResolver
{
    private readonly IReadOnlyList<CallableOwner> _owners;
    private readonly CallableLookup _lookup;

    public CallResolver(IReadOnlyList<CallableOwner> owners)
    {
        _owners = owners;
        _lookup = new CallableLookup(owners);
    }

    public CallableOwner? Resolve(CallableOwner caller, InvocationExpressionSyntax call)
    {
        if (call.Expression is IdentifierNameSyntax bare)
            return ResolveBare(caller, bare, call.ArgumentList.Arguments.Count);
        if (call.Expression is ParenthesizedExpressionSyntax grouped)
            return CallableNodeLookup.Find(_owners, grouped);
        if (call.Expression is InvocationExpressionSyntax factory)
            return ResolveFactoryReturn(caller, factory);
        return call.Expression is MemberAccessExpressionSyntax member
            ? _lookup.MemberTarget(caller, member, call.ArgumentList.Arguments.Count) : null;
    }

    private CallableOwner? ResolveBare(CallableOwner caller, IdentifierNameSyntax name, int arity)
    {
        if (_lookup.VisibleLocal(caller, name, arity) is { } local) return local;
        if (LocalShadowing.IsShadowedByLocal(name, name.Identifier.Text))
            return BoundDelegate(caller, name);
        return _lookup.SameClassMethod(caller, name, arity);
    }

    private CallableOwner? BoundDelegate(CallableOwner caller, IdentifierNameSyntax name) =>
        CallableBindings.Value(caller, name) is { } value ? ResolveDelegateValue(caller, CallableSyntax.Unwrap(value)) : null;

    private CallableOwner? ResolveDelegateValue(CallableOwner caller, ExpressionSyntax value)
    {
        if (value is IdentifierNameSyntax method) return ResolveBare(caller, method, 0);
        if (value is InvocationExpressionSyntax factory)
            return ResolveFactoryReturn(caller, factory);
        return CallableNodeLookup.Find(_owners, value);
    }

    private CallableOwner? ResolveFactoryReturn(CallableOwner caller, InvocationExpressionSyntax factory) =>
        Resolve(caller, factory) is { } returnedFactory ? ReturnedCallableResolver.Find(returnedFactory) : null;

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
