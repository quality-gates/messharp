using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageFlow(StorageContext context)
{
    public StorageEnvironment Before(SyntaxNode site, StorageEnvironment initial, bool after = false) =>
        new BindingOrigins(context.Body, Origin, Execute, Callable, context.Weak).Before(site, initial, after);

    private StorageCallable? Callable(ExpressionSyntax value, StorageEnvironment state) =>
        new StorageCallables(context, expression => Origin(expression, state), state).Find(value);

    public StorageOrigin Origin(ExpressionSyntax expression, StorageEnvironment state)
    {
        var weak = context.Weak || BodyBindingsCache.For(context.Body).MayBeSkipped(expression);
        if (new StorageDeferred(context, item => Origin(item, state), state).Value(CallableSyntax.Unwrap(expression), weak) is { } deferred)
            return deferred;
        var evaluation = new StorageEvaluations(context, item => Origin(item, state), state);
        StorageOrigin? Call(InvocationExpressionSyntax call) => evaluation.Invoke(call,
            context.Weak || BodyBindingsCache.For(context.Body).MayBeSkipped(call));
        return new StorageOriginResolver(context, state, evaluation.Fresh, Call).Origin(expression);
    }

    private bool Execute(SyntaxNode node, StorageEnvironment state, bool weak) =>
        StorageStepExecutor.Execute(context, expression => Origin(expression, state), node, state, weak);
}

internal static class StorageStepExecutor
{
    public static bool Execute(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, SyntaxNode node,
        StorageEnvironment state, bool weak)
    {
        if (new StorageReferences(context, origin, state).Bind(node, weak)) return true;
        if (new StorageDeferred(context, origin, state).Execute(node, weak)) return true;
        if (node is InvocationExpressionSyntax call)
        {
            new StorageEvaluations(context, origin, state).Invoke(call, weak);
            return true;
        }
        return node is AssignmentExpressionSyntax assignment
            && new StorageWrites(context, origin, state).Assign(assignment, weak);
    }
}
