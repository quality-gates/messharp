using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageParameterBindings(StorageContext context, StorageCallFrame frame,
    InvocationExpressionSyntax call, StorageEnvironment caller, Func<ExpressionSyntax, StorageOrigin> origin)
{
    public void Apply(StorageEnvironment state)
    {
        var target = frame.Target;
        var callables = new StorageCallables(context, origin, caller);
        var ownCount = target.Parent is null ? target.Method.Parameters.Count : CallableOwner.Parameters(target.Method.Node).Count();
        var count = target.Method.Parameters.Count;
        for (var index = 0; index < count; index++)
        {
            var name = target.Method.Parameters[index].Name;
            var capture = index >= ownCount;
            var actual = capture ? SyntaxFactory.IdentifierName(name) : CallArguments.Actual(target, call, name, index);
            state.Bindings[name] = Bound(name, capture, actual);
            CaptureReference(state, name, capture);
            if (actual is not null && callables.Find(actual) is { } callable) state.Callables[name] = callable;
        }
    }

    private void CaptureReference(StorageEnvironment state, string name, bool capture)
    {
        if (capture && frame.Callable?.References.TryGetValue(name, out var reference) == true)
            state.References[name] = reference;
    }

    private StorageOrigin Bound(string name, bool capture, ExpressionSyntax? actual)
    {
        if (capture && frame.Callable?.Captures.TryGetValue(name, out var bound) == true) return bound;
        return actual is null ? StorageOrigin.Unknown : origin(actual);
    }
}
