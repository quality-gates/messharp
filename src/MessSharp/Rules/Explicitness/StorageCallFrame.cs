using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed record StorageCallFrame(CallableOwner Target, StorageOrigin? Receiver, StorageCallable? Callable = null)
{
    public static StorageCallFrame? Resolve(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin,
        StorageEnvironment state, InvocationExpressionSyntax call)
    {
        if (context.Owner is not { } owner) return null;
        var receiver = call.Expression is MemberAccessExpressionSyntax member ? origin(member.Expression)
            : state.Bindings.GetValueOrDefault(StorageContext.ThisBinding);
        if (call.Expression is IdentifierNameSyntax name && state.Callables.TryGetValue(name.Identifier.Text, out var bound))
            return new StorageCallFrame(bound.Target, receiver, bound);
        if (new CallResolver(context.Owners).Resolve(owner, call) is not { } target) return null;
        var callable = target.Parent is null ? null : new StorageCallables(context, origin, state).Capture(target);
        return new StorageCallFrame(target, receiver, callable);
    }
}
