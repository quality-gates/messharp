using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// One resolution of a deferred value: follows bindings, calls, branches, elements, parameter entries and
/// written arguments to the work they hold. A value revisited while it is being resolved is unknown.
/// </summary>
internal sealed class DeferredValueWalk(IReadOnlyList<CallableOwner> owners, int arity = 0,
    HashSet<SyntaxNode>? visiting = null)
{
    private static readonly DeferredResolution Unknown = new([], false);
    private static readonly DeferredResolution Nothing = new([], true);
    private readonly HashSet<SyntaxNode> _visiting = visiting ?? [];

    public DeferredResolution Resolve(DeferredValueContext context, ExpressionSyntax value, DeferredKind kind)
    {
        value = CallableSyntax.Unwrap(value);
        if (!_visiting.Add(value)) return Unknown;
        try { return ResolveValue(context, value, kind); }
        finally { _visiting.Remove(value); }
    }

    private DeferredResolution ResolveValue(DeferredValueContext context, ExpressionSyntax value, DeferredKind kind) =>
        value switch
        {
            IdentifierNameSyntax or MemberAccessExpressionSyntax => ResolveBindings(context, value, kind),
            InvocationExpressionSyntax call => new DeferredCallResolver(owners, Resolve, Callables).Resolve(context, call, kind),
            ElementAccessExpressionSyntax element => ResolveElements(context, element.Expression, kind),
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } or DefaultExpressionSyntax => Nothing,
            _ when Branches(value) is { } branches => Merge(context, branches, kind),
            _ => ResolveCreation(context, value, kind),
        };

    /// <summary>The values a conditional, switch or coalescing expression can produce.</summary>
    private static IReadOnlyList<ExpressionSyntax>? Branches(ExpressionSyntax value) => value switch
    {
        ConditionalExpressionSyntax choice => [choice.WhenTrue, choice.WhenFalse],
        SwitchExpressionSyntax choice => choice.Arms.Select(arm => arm.Expression).ToList(),
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce => [coalesce.Left, coalesce.Right],
        _ => null,
    };

    private DeferredResolution Merge(DeferredValueContext context, IReadOnlyList<ExpressionSyntax> values, DeferredKind kind) =>
        DeferredResolution.Merge(values.Select(value => Resolve(context, value, kind)));

    private DeferredResolution ResolveElements(DeferredValueContext context, ExpressionSyntax collection, DeferredKind kind) =>
        DeferredElements.ElementsOf(context, collection) is { } elements ? Merge(context, elements, kind) : Unknown;

    private DeferredResolution ResolveBindings(DeferredValueContext context, ExpressionSyntax value, DeferredKind kind)
    {
        var sources = context.Values(value);
        if (sources is { Count: > 0 }) return DeferredResolution.Merge(sources.Select(source => ResolveSource(context, source, kind)));
        if (sources is null && kind == DeferredKind.Call) return CallableWork.Group(owners, context, value, arity);
        return new DeferredResolution([], !KnownExecutionOperations.RequiresExecution(context.Owner, value, kind));
    }

    private DeferredResolution ResolveSource(DeferredValueContext context, DeferredSource source, DeferredKind kind) =>
        source switch
        {
            DeferredSource.Value { Owner: null } value => Resolve(context, value.Expression, kind),
            DeferredSource.Value { Owner.Body: not null } value => Resolve(context.For(value.Owner), value.Expression, kind),
            DeferredSource.Entry entry => ResolveEntry(context, entry.Parameter, kind),
            DeferredSource.Element element => ResolveElements(context, element.Collection, kind),
            DeferredSource.Written written => DeferredArgumentExits.Find(owners, context, written.Argument) is { } exit
                ? DeferredResolution.Merge(exit.Sources.Select(item => ResolveSource(exit.Context, item, kind)))
                : Unknown,
            _ => Unknown,
        };

    private DeferredResolution ResolveEntry(DeferredValueContext context, string parameter, DeferredKind kind)
    {
        if (context.Argument(parameter) is { } argument) return Resolve(context.Caller!.Context, argument, kind);
        if (kind == DeferredKind.Sequence)
            return new DeferredResolution([], !KnownExecutionOperations.RequiresExecution(context.Owner,
                SyntaxFactory.IdentifierName(parameter), kind));
        return context.Caller is null && !context.Foreign ? DeferredResolution.Parameter(parameter) : Unknown;
    }

    private DeferredResolution ResolveCreation(DeferredValueContext context, ExpressionSyntax value, DeferredKind kind)
    {
        if (kind == DeferredKind.Call) return CallableWork.Creation(owners, context, value);
        if (LazyValueResolver.Resolve(owners, context, value, Callables) is { } lazy) return lazy;
        return new DeferredResolution([], !KnownExecutionOperations.RequiresExecution(context.Owner, value, kind));
    }

    /// <summary>The callables a delegate value holds, resolved within this walk so a revisited value stays unknown.</summary>
    private DeferredResolution Callables(DeferredValueContext context, ExpressionSyntax value, int callArity) =>
        new DeferredValueWalk(owners, callArity, _visiting).Resolve(context, value, DeferredKind.Call);
}
