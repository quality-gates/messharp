using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// What a deferred value runs: the elements of a sequence, the factory of a Lazy, or the callable a delegate
/// holds when invoked.
/// </summary>
internal enum DeferredKind { Sequence, Lazy, Call }

internal sealed record DeferredWork(DeferredKind Kind, CallableOwner Target, InvocationExpressionSyntax Call,
    IReadOnlyDictionary<string, StorageOrigin> Bindings, DeferredValueContext? Frame = null)
{
    /// <summary>Forcing a Lazy whose factory is an iterator only creates the sequence; iterating it runs the body.</summary>
    public bool RunsWhenForced(DeferredKind forced) =>
        Kind == forced && !(forced == DeferredKind.Lazy && KnownExecutionOperations.IsIterator(Target));

    public CallEdge CallbackEdge(CallEdge edge, InvocationExpressionSyntax call, IReadOnlyList<StorageOrigin> arguments)
    {
        var bindings = this.Captured(edge.Site);
        foreach (var (name, index) in Target.OwnParameters().Take(arguments.Count).Select((name, index) => (name, index)))
            bindings[name] = arguments[index];
        return new CallEdge(edge.Caller, Target, call, edge.Site, bindings);
    }
}

/// <summary>Resolves the deferred work a value runs when executed, starting from a caller's body.</summary>
internal sealed class DeferredValueResolver(RuleContext ctx, IReadOnlyList<CallableOwner> owners)
{
    public DeferredResolution Resolve(CallableOwner caller, ExpressionSyntax value, SyntaxNode body, DeferredKind kind) =>
        new DeferredValueWalk(owners).Resolve(new DeferredValueContext(ctx, owners, caller, body), value, kind);

    /// <summary>What the elements of the caller's collection hold, as work of <paramref name="kind"/>.</summary>
    public DeferredResolution Elements(CallableOwner caller, ExpressionSyntax collection, SyntaxNode body, DeferredKind kind) =>
        new DeferredValueWalk(owners).ResolveElements(new DeferredValueContext(ctx, owners, caller, body), collection, kind);

    /// <summary>The callables a delegate value can hold when it is invoked with <paramref name="arity"/> arguments.</summary>
    public DeferredResolution Callables(CallableOwner caller, ExpressionSyntax value, SyntaxNode body, int arity) =>
        new DeferredValueWalk(owners, arity).Resolve(new DeferredValueContext(ctx, owners, caller, body), value, DeferredKind.Call);

    /// <summary>
    /// The callables the caller's argument can hold. A captured delegate was resolved where the callee reads it,
    /// so only its entry value is left: the caller's own parameter or capture of the same name.
    /// </summary>
    public DeferredResolution Held(CallableOwner caller, ExpressionSyntax actual, SyntaxNode body, int arity)
    {
        if (actual.Parent is not null) return Callables(caller, actual, body, arity);
        return actual is IdentifierNameSyntax name && caller.Method.Parameters.Any(p => p.Name == name.Identifier.Text)
            ? DeferredResolution.Parameter(name.Identifier.Text) : new DeferredResolution([], false);
    }
}
