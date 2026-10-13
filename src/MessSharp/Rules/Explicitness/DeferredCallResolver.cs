using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class DeferredCallResolver(
    IReadOnlyList<CallableOwner> owners,
    Func<DeferredValueContext, ExpressionSyntax, DeferredKind, DeferredResolution> resolve)
{
    public DeferredResolution Resolve(DeferredValueContext context, InvocationExpressionSyntax call, DeferredKind kind)
    {
        if (KnownExecutionOperations.IsColdQuery(context.Owner, call)) return QueryWork(context, call);
        var target = new CallResolver(owners).Resolve(context.Owner, call);
        if (target?.Body is not { } body)
            return new DeferredResolution([], !KnownExecutionOperations.RequiresExecution(context.Owner, call, kind));
        var bound = context.Bind(target, call, body);
        if (KnownExecutionOperations.IsIterator(target))
            return new DeferredResolution([new DeferredWork(DeferredKind.Sequence, target, call, bound.Bindings)], true);
        return DeferredResolution.Merge(CallableSyntax.Returns(body).Select(returned => resolve(bound, returned, kind)));
    }

    private DeferredResolution QueryWork(DeferredValueContext context, InvocationExpressionSyntax call)
    {
        var member = (MemberAccessExpressionSyntax)call.Expression;
        var source = resolve(context, member.Expression, DeferredKind.Sequence);
        if (call.ArgumentList.Arguments.FirstOrDefault()?.Expression is not { } callback) return source with { Complete = false };
        var target = new CallbackResolver(owners).Find(context.Owner, callback);
        if (target is null) return source with { Complete = false };
        var bindings = context.Captures(target);
        var parameter = CallableOwner.Parameters(target.Method.Node).FirstOrDefault();
        if (parameter is not null) bindings[parameter.Identifier.Text] = context.Storage(member.Expression).Element();
        return source with { Work = [.. source.Work, new DeferredWork(DeferredKind.Sequence, target, call, bindings)] };
    }

}
