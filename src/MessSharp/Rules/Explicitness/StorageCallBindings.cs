using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class StorageCallBindings
{
    public static string? Root(CallableOwner target, ExpressionSyntax expression)
    {
        var parameters = StateNames.Parameters(target.Method.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal));
        if (parameters.Resolve(expression) is { } parameter) return parameter;
        if (target.Method.Class is not { } cls) return null;
        var statics = StateNames.StaticMembers(cls.Fields.Where(f => f.IsStatic).Select(f => f.Name).ToHashSet(), cls.Name);
        var members = StateNames.InstanceMembers(cls.Fields.Where(f => !f.IsStatic).Select(f => f.Name).ToHashSet());
        return statics.Resolve(expression) is { } shared ? StorageRoots.Static(shared)
            : members.Resolve(expression) is { } member ? StorageRoots.Member(member) : null;
    }

    public static StorageEnvironment Bind(StorageContext context, StorageCallFrame frame, InvocationExpressionSyntax call,
        StorageEnvironment caller, Func<ExpressionSyntax, StorageOrigin> origin)
    {
        var target = frame.Target;
        var state = caller.Copy();
        state.Bindings.Clear();
        state.Callables.Clear();
        var callables = new StorageCallables(context, origin, caller);
        var ownCount = target.Parent is null ? target.Method.Parameters.Count
            : CallableOwner.Parameters(target.Method.Node).Count();
        var parameterCount = target.Method.Parameters.Count;
        for (var index = 0; index < parameterCount; index++)
        {
            var name = target.Method.Parameters[index].Name;
            var actual = index >= ownCount ? Microsoft.CodeAnalysis.CSharp.SyntaxFactory.IdentifierName(name)
                : CallArguments.Actual(call, name, index);
            state.Bindings[name] = Bound(frame, name, index >= ownCount, actual, origin);
            if (actual is not null && callables.Find(actual) is { } callable) state.Callables[name] = callable;
        }
        state.Heap = caller.Heap.Copy();
        if (frame.Receiver is { } receiver) state.Bindings[StorageContext.ThisBinding] = receiver;
        return state;
    }

    private static StorageOrigin Bound(StorageCallFrame frame, string name, bool capture,
        ExpressionSyntax? actual, Func<ExpressionSyntax, StorageOrigin> origin)
    {
        if (capture && frame.Callable?.Captures.TryGetValue(name, out var bound) == true) return bound;
        return actual is null ? StorageOrigin.Unknown : origin(actual);
    }
}
