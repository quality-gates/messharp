using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class DeferredCallResolver(
    IReadOnlyList<CallableOwner> owners,
    Func<DeferredValueContext, ExpressionSyntax, DeferredKind, DeferredResolution> resolve,
    Func<DeferredValueContext, ExpressionSyntax, int, DeferredResolution> callables)
{
    public DeferredResolution Resolve(DeferredValueContext context, InvocationExpressionSyntax call, DeferredKind kind)
    {
        if (KnownExecutionOperations.IsColdQuery(context.Owner, call)) return QueryWork(context, call);
        return new CallResolver(owners).Resolve(context.Owner, call) is { } target
            ? Returned(context, target, call, kind) : Delegated(context, call, kind);
    }

    private DeferredResolution Returned(DeferredValueContext context, CallableOwner target, InvocationExpressionSyntax call,
        DeferredKind kind)
    {
        if (target.Body is not { } body) return Opaque(context, call, kind);
        var bound = context.Bind(target, call, body);
        if (KnownExecutionOperations.IsIterator(target))
            return new DeferredResolution([new DeferredWork(DeferredKind.Sequence, target, call, bound.Bindings)], true);
        var returned = DeferredResolution.Merge(CallableSyntax.Returns(body).Select(value => resolve(bound, value, kind)));
        return returned with { Work = returned.Work.Select(work => work.Escaping(target)).ToList() };
    }

    /// <summary>A call through a delegate returns whatever each callable the delegate can hold returns.</summary>
    private DeferredResolution Delegated(DeferredValueContext context, InvocationExpressionSyntax call, DeferredKind kind)
    {
        if (DelegateCallee.Value(call) is not { } callee) return Opaque(context, call, kind);
        var held = callables(context, callee, call.ArgumentList.Arguments.Count);
        if (held.Work.Count == 0) return Opaque(context, call, kind);
        var returned = DeferredResolution.Merge(held.Work.Select(work => Returned(context, work.Target, call, kind)));
        return held.Resolved ? returned : returned with { Complete = false };
    }

    private static DeferredResolution Opaque(DeferredValueContext context, InvocationExpressionSyntax call, DeferredKind kind) =>
        new([], !KnownExecutionOperations.RequiresExecution(context.Owner, call, kind));

    private DeferredResolution QueryWork(DeferredValueContext context, InvocationExpressionSyntax call)
    {
        var member = (MemberAccessExpressionSyntax)call.Expression;
        var source = resolve(context, member.Expression, DeferredKind.Sequence);
        if (call.ArgumentList.Arguments.FirstOrDefault()?.Expression is not { } callback) return source with { Complete = false };
        var held = callables(context, callback, 1);
        var element = context.Storage(member.Expression).Element();
        var work = held.Work.Select(item => new DeferredWork(DeferredKind.Sequence, item.Target, call, Element(item, element)));
        return source with { Work = [.. source.Work, .. work], Complete = source.Complete && held.Resolved };
    }

    private static Dictionary<string, StorageOrigin> Element(DeferredWork work, StorageOrigin element)
    {
        var bindings = new Dictionary<string, StorageOrigin>(work.Bindings);
        if (CallableOwner.Parameters(work.Target.Method.Node).FirstOrDefault() is { } parameter)
            bindings[parameter.Identifier.Text] = element;
        else if (work.Target.Parent is null && work.Target.Method.Parameters.FirstOrDefault() is { } declared)
            bindings[declared.Name] = element;
        return bindings;
    }
}
