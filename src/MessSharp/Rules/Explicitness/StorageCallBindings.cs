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
        var state = caller.Copy();
        state.Bindings.Clear();
        state.Callables.Clear();
        state.References.Clear();
        new StorageParameterBindings(context, frame, call, caller, origin).Apply(state);
        state.Heap = caller.Heap.Copy();
        if (frame.Receiver is { } receiver) state.Bindings[StorageContext.ThisBinding] = receiver;
        return state;
    }

}
