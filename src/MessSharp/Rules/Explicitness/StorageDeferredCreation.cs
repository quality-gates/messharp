using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageDeferredCreation(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    public StorageOrigin? Iterator(InvocationExpressionSyntax call)
    {
        var frame = StorageCallFrame.Resolve(context, origin, state, call);
        if (frame?.Target.Body is null || !KnownExecutionOperations.IsIterator(frame.Target)) return null;
        var initial = StorageCallBindings.Bind(context, frame, call, state, origin);
        return Allocate(call, new StorageDeferredValue(DeferredKind.Sequence, [new StorageDeferredWork(frame.Target, initial)]));
    }

    public void Lazy(BaseObjectCreationExpressionSyntax creation, StorageOrigin receiver)
    {
        if (LazyFactory(creation) is not { } callback) return;
        var work = new StorageCallbackWork(context, origin, state).Create(callback, creation, StorageOrigin.Private);
        if (work is not null) Register(receiver, new StorageDeferredValue(DeferredKind.Lazy, [work]));
    }

    private ExpressionSyntax? LazyFactory(BaseObjectCreationExpressionSyntax creation)
    {
        if (context.Owner is not { } owner || !KnownExecutionOperations.IsLazy(owner, creation)) return null;
        var factory = LazyFactories.Select(owner, creation);
        return factory.Kind == LazyFactoryKind.Factory ? factory.Expression : null;
    }

    public StorageOrigin? Query(InvocationExpressionSyntax call)
    {
        if (context.Owner is not { } owner || !KnownExecutionOperations.IsColdQuery(owner, call)
            || call.Expression is not MemberAccessExpressionSyntax member) return null;
        var source = origin(member.Expression);
        var work = new StorageCallbackWork(context, origin, state).From(call, state.Heap.Read(source, StorageCell.AnyElement));
        return Allocate(call, new StorageDeferredValue(DeferredKind.Sequence, work, source, member.Name.Identifier.Text == "Where"));
    }

    private StorageOrigin Allocate(ExpressionSyntax expression, StorageDeferredValue value)
    {
        var receiver = new StorageAllocations(context, state).Allocate(expression, new StorageCell());
        Register(receiver, value);
        return receiver;
    }

    private void Register(StorageOrigin receiver, StorageDeferredValue value)
    {
        foreach (var identity in receiver.Objects) state.Deferred[identity] = value;
    }
}
