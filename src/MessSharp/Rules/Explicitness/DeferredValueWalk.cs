using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// One resolution of a deferred value: follows bindings, calls, branches, elements, parameter entries and
/// written arguments to the work they hold. A value revisited while it is being resolved is unknown.
/// </summary>
internal sealed class DeferredValueWalk(IReadOnlyList<CallableOwner> owners, int arity = 0,
    ActiveResolutions? visiting = null)
{
    private static readonly DeferredResolution Unknown = new([], false);
    private static readonly DeferredResolution Nothing = new([], true);
    private readonly ActiveResolutions _visiting = visiting ?? new();

    public DeferredResolution Resolve(DeferredValueContext context, ExpressionSyntax value, DeferredKind kind)
    {
        value = CallableSyntax.Unwrap(value);
        if (_visiting.Enter(context, value) is { } revisit) return revisit;
        try { return ResolveValue(context, value, kind); }
        finally { _visiting.Leave(value); }
    }

    private DeferredResolution ResolveValue(DeferredValueContext context, ExpressionSyntax value, DeferredKind kind) =>
        value switch
        {
            IdentifierNameSyntax or MemberAccessExpressionSyntax => ResolveName(context, value, kind),
            InvocationExpressionSyntax call => new DeferredCallResolver(owners, Resolve, Callables).Resolve(context, call, kind),
            ElementAccessExpressionSyntax element => ResolveElements(context, element.Expression, kind),
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } or DefaultExpressionSyntax => Nothing,
            _ when ValueBranches.Find(value) is { } branches => Merge(context, branches, kind),
            _ => ResolveCreation(context, value, kind),
        };

    private DeferredResolution ResolveName(DeferredValueContext context, ExpressionSyntax value, DeferredKind kind) =>
        kind == DeferredKind.Sequence && value is MemberAccessExpressionSyntax { Name.Identifier.Text: "Value" } member
            && KnownExecutionOperations.IsLazyValue(context.Owner, member.Expression)
            ? ResolveLazyResult(context, member.Expression) : ResolveBindings(context, value, kind);

    /// <summary>A sequence read from a Lazy's value is whatever its factories return; forcing it only creates the sequence.</summary>
    private DeferredResolution ResolveLazyResult(DeferredValueContext context, ExpressionSyntax lazy)
    {
        var factories = Resolve(context, lazy, DeferredKind.Lazy);
        var calls = new DeferredCallResolver(owners, Resolve, Callables);
        var returned = DeferredResolution.Merge(factories.Work.Select(work =>
            calls.Returned(context, work.Target, work.Call, DeferredKind.Sequence)));
        return factories.Resolved ? returned : returned with { Complete = false };
    }

    private DeferredResolution Merge(DeferredValueContext context, IReadOnlyList<ExpressionSyntax> values, DeferredKind kind) =>
        DeferredResolution.Merge(values.Select(value => Resolve(context, value, kind)));

    /// <summary>What the elements of <paramref name="collection"/> hold, as work of <paramref name="kind"/>.</summary>
    public DeferredResolution ResolveElements(DeferredValueContext context, ExpressionSyntax collection, DeferredKind kind) =>
        DeferredElements.ElementsOf(context, collection) is { } elements ? Merge(context, elements, kind)
            : ResolveParameterElements(context, collection, kind);

    /// <summary>The elements of a read-only collection parameter are the elements of the caller's argument.</summary>
    private DeferredResolution ResolveParameterElements(DeferredValueContext context, ExpressionSyntax collection, DeferredKind kind)
    {
        if (DeferredElements.ReadOnlyParameter(context, collection) is not { } parameter) return Unknown;
        if (context.Argument(parameter) is { } argument) return ResolveElements(context.Caller!.Context, argument, kind);
        return context.Caller is null && !context.Foreign ? DeferredResolution.ElementsOfParameter(parameter) : Unknown;
    }

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
