using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageEvaluations(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    public StorageOrigin? Fresh(ExpressionSyntax expression) => Cached(expression,
        () => new StorageCreation(context, origin, state).Create(expression));

    public StorageOrigin? Invoke(InvocationExpressionSyntax call, bool weak) => Cached(call, () =>
    {
        if (new StorageCollections(context, origin, state).Mutate(call, weak)) return StorageOrigin.Private;
        return new StorageCalls(context, origin, state).Invoke(call, weak);
    });

    private StorageOrigin? Cached(ExpressionSyntax expression, Func<StorageOrigin?> evaluate)
    {
        var identity = context.Identity(expression);
        if (state.Calls.TryGetValue(identity, out var cached)) return cached;
        var value = evaluate();
        if (value is not null) state.Calls[identity] = value;
        return value;
    }
}
