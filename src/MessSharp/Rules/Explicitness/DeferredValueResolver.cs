using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal enum DeferredKind { Sequence, Lazy }

internal sealed record DeferredWork(DeferredKind Kind, CallableOwner Target, InvocationExpressionSyntax Call,
    IReadOnlyDictionary<string, StorageOrigin> Bindings);

/// <summary>Resolves the deferred work a value runs when executed, starting from a caller's body.</summary>
internal sealed class DeferredValueResolver(RuleContext ctx, IReadOnlyList<CallableOwner> owners)
{
    public DeferredResolution Resolve(CallableOwner caller, ExpressionSyntax value, SyntaxNode body, DeferredKind kind) =>
        new DeferredValueWalk(owners).Resolve(new DeferredValueContext(ctx, owners, caller, body), value, kind);
}
