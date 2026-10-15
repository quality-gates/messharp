using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageCalls(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    public StorageOrigin? Invoke(InvocationExpressionSyntax call, bool weak)
    {
        var frame = StorageCallFrame.Resolve(context, origin, state, call);
        if (frame?.Target.Body is not { } body) return null;
        return Execute(frame, body, call, call, weak);
    }

    public void Construct(ConstructorCall constructor, SyntaxNode site, StorageOrigin receiver, bool weak)
    {
        if (constructor.Target.Body is { } body)
            Execute(new StorageCallFrame(constructor.Target, receiver), body, constructor.Invocation, site, weak);
    }

    private StorageOrigin Execute(StorageCallFrame frame, SyntaxNode body, InvocationExpressionSyntax call, SyntaxNode site, bool weak)
        => Execute(frame.Target, body, site, weak, StorageCallBindings.Bind(context, frame, call, state, origin));

    public StorageOrigin ExecuteDeferred(StorageDeferredWork work, SyntaxNode site, bool weak)
    {
        if (work.Target.Body is not { } body) return StorageOrigin.Unknown;
        var initial = work.Initial.Copy();
        initial.Heap = state.Heap.Copy();
        foreach (var pair in state.Deferred) initial.Deferred[pair.Key] = pair.Value;
        return Execute(work.Target, body, site, weak, initial);
    }

    private StorageOrigin Execute(CallableOwner target, SyntaxNode body, SyntaxNode site, bool weak, StorageEnvironment initial)
    {
        if (context.Visiting.Contains(target)) return StorageOrigin.Unknown;
        var result = StorageCallMemo.For(context.For(target, body, site, weak), target, weak, initial);
        if (weak) state.Heap.Join(result.Heap);
        else state.Heap = result.Heap.Copy();
        foreach (var pair in result.Deferred) state.Deferred[pair.Key] = pair.Value;
        return result.Returned;
    }
}
