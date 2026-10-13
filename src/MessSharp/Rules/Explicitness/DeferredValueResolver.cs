using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal enum DeferredKind { Sequence, Lazy }

internal sealed record DeferredWork(DeferredKind Kind, CallableOwner Target, InvocationExpressionSyntax Call,
    IReadOnlyDictionary<string, StorageOrigin> Bindings);

internal sealed class DeferredValueResolver(RuleContext ctx, IReadOnlyList<CallableOwner> owners)
{
    public DeferredResolution Resolve(CallableOwner caller, ExpressionSyntax value, SyntaxNode body, DeferredKind kind) =>
        Resolve(new DeferredValueContext(ctx, owners, caller, body), value, kind, new HashSet<SyntaxNode>());

    private DeferredResolution Resolve(DeferredValueContext context, ExpressionSyntax value, DeferredKind kind,
        HashSet<SyntaxNode> visiting)
    {
        value = CallableSyntax.Unwrap(value);
        if (!visiting.Add(value)) return new DeferredResolution([], false);
        try { return ResolveValue(context, value, kind, visiting); }
        finally { visiting.Remove(value); }
    }

    private DeferredResolution ResolveValue(DeferredValueContext context, ExpressionSyntax value, DeferredKind kind,
        HashSet<SyntaxNode> visiting) => value switch
        {
            IdentifierNameSyntax or MemberAccessExpressionSyntax =>
                ResolveBindings(context, value, kind, visiting),
            InvocationExpressionSyntax call => new DeferredCallResolver(owners,
                (bound, returned, execution) => Resolve(bound, returned, execution, visiting)).Resolve(context, call, kind),
            ConditionalExpressionSyntax choice => DeferredResolution.Merge([
                Resolve(context, choice.WhenTrue, kind, visiting), Resolve(context, choice.WhenFalse, kind, visiting)]),
            SwitchExpressionSyntax choice => DeferredResolution.Merge(
                choice.Arms.Select(arm => Resolve(context, arm.Expression, kind, visiting))),
            _ => ResolveCreation(context, value, kind),
        };

    private DeferredResolution ResolveBindings(DeferredValueContext context, ExpressionSyntax value, DeferredKind kind,
        HashSet<SyntaxNode> visiting)
    {
        var bindings = context.Values(value).ToList();
        return bindings.Count > 0
            ? DeferredResolution.Merge(bindings.Select(bound => Resolve(context, bound, kind, visiting)))
            : new DeferredResolution([], !KnownExecutionOperations.RequiresExecution(context.Owner, value, kind));
    }

    private DeferredResolution ResolveCreation(DeferredValueContext context, ExpressionSyntax value, DeferredKind kind)
    {
        var work = LazyValueResolver.Resolve(owners, context, value).ToList();
        return new DeferredResolution(work, work.Count > 0
            || !KnownExecutionOperations.RequiresExecution(context.Owner, value, kind));
    }

}
