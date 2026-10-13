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

    private CallableOwner? BoundDelegate(CallableOwner caller, IdentifierNameSyntax name)
    {
        if (ExpressionTypes.Binding(name) is not { Initializer: { } initializer } binding) return null;
        if (ReassignedBeforeUse(caller, name, binding)) return null;
        return ResolveDelegateValue(caller, CallableSyntax.Unwrap(initializer.Value));
    }

    private CallableOwner? ResolveDelegateValue(CallableOwner caller, ExpressionSyntax value)
    {
        if (value is IdentifierNameSyntax method) return ResolveBare(caller, method, 0);
        if (value is InvocationExpressionSyntax factory)
            return ResolveFactoryReturn(caller, factory);
        return CallableNodeLookup.Find(_owners, value);
    }

    private CallableOwner? ResolveFactoryReturn(CallableOwner caller, InvocationExpressionSyntax factory) =>
        Resolve(caller, factory) is { } returnedFactory ? ReturnedCallableResolver.Find(returnedFactory) : null;

    private static bool ReassignedBeforeUse(
        CallableOwner caller, IdentifierNameSyntax name, VariableDeclaratorSyntax binding) =>
        caller.Body is not null && caller.Body.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(a =>
            a.Left is IdentifierNameSyntax target && target.Identifier.Text == name.Identifier.Text
                && ExpressionTypes.Binding(target) == binding && a.SpanStart > binding.SpanStart
                && (a.SpanStart < name.SpanStart || RepeatsAt(a, name) || Nested(a, caller)));

    private static bool RepeatsAt(AssignmentExpressionSyntax assignment, IdentifierNameSyntax site) =>
        assignment.Ancestors().Any(node => node is ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax
            && node.Span.Contains(site.Span));

    private static bool Nested(AssignmentExpressionSyntax assignment, CallableOwner owner) =>
        assignment.Ancestors().TakeWhile(node => node != owner.Body)
            .Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
}

internal static class CallableSyntax
{
    public static ExpressionSyntax Unwrap(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax grouped => Unwrap(grouped.Expression),
        CastExpressionSyntax cast => Unwrap(cast.Expression),
        _ => expression,
    };
}
