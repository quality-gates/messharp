using System.Collections.Immutable;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The caller frame a bound context reads parameter entry values from.</summary>
internal sealed record DeferredCaller(DeferredValueContext Context, InvocationExpressionSyntax Call);

/// <summary>
/// One owner body in which deferred values are resolved. A bound context knows its caller, so a parameter's
/// entry value is the caller's argument. A foreign context evaluates a writer found elsewhere: no caller is
/// known there, so its parameters stay unknown.
/// </summary>
internal sealed class DeferredValueContext(
    RuleContext ctx, IReadOnlyList<CallableOwner> owners, CallableOwner owner, SyntaxNode body,
    IReadOnlyDictionary<string, StorageOrigin> bindings, DeferredCaller? caller = null, bool foreign = false)
{
    public DeferredValueContext(RuleContext ctx, IReadOnlyList<CallableOwner> owners, CallableOwner owner, SyntaxNode body)
        : this(ctx, owners, owner, body, ImmutableDictionary<string, StorageOrigin>.Empty) { }

    public CallableOwner Owner { get; } = owner;
    public SyntaxNode Body { get; } = body;
    public IReadOnlyDictionary<string, StorageOrigin> Bindings { get; } = bindings;
    public DeferredCaller? Caller { get; } = caller;
    public bool Foreign { get; } = foreign;

    public IReadOnlyList<DeferredSource>? Values(ExpressionSyntax reference) =>
        new DeferredValueBindings(Owner, Body, owners).Values(reference);

    public DeferredValueContext Bind(CallableOwner target, InvocationExpressionSyntax call, SyntaxNode targetBody)
    {
        var bound = target.Method.Parameters.Select((parameter, index) =>
            (parameter.Name, Value: CallArguments.Actual(target, call, parameter.Name, index)))
            .ToDictionary(pair => pair.Name, pair => pair.Value is null ? StorageOrigin.Unknown : Storage(pair.Value));
        return new DeferredValueContext(ctx, owners, target, targetBody, bound, new DeferredCaller(this, call));
    }

    /// <summary>A root context for a writer that <paramref name="writer"/> evaluates on its own.</summary>
    public DeferredValueContext For(CallableOwner writer) =>
        new(ctx, owners, writer, writer.Body!, ImmutableDictionary<string, StorageOrigin>.Empty, foreign: true);

    /// <summary>The caller's argument for an own parameter of a bound context, or null when unknown.</summary>
    public ExpressionSyntax? Argument(string parameter)
    {
        if (Caller is null) return null;
        var index = Owner.Method.Parameters.FindIndex(p => p.Name == parameter);
        var own = Owner.Parent is null ? Owner.Method.Parameters.Count : CallableOwner.Parameters(Owner.Method.Node).Count();
        return index >= 0 && index < own ? CallArguments.Actual(Owner, Caller.Call, parameter, index) : null;
    }

    public StorageOrigin Storage(ExpressionSyntax value)
    {
        var origin = CallArguments.Ownership(ctx, Owner, owners, Body).StorageAt(value, value);
        return StorageProjection.Apply(origin, root => Bindings.GetValueOrDefault(root) ?? StorageOrigin.Shared(root));
    }
}
