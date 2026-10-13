using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageFlow(StorageContext context)
{
    public StorageEnvironment Before(SyntaxNode site, StorageEnvironment initial, bool after = false) =>
        new BindingOrigins(context.Body, Origin, Execute, context.Weak).Before(site, initial, after);

    public StorageOrigin Origin(ExpressionSyntax expression, StorageEnvironment state)
    {
        var weak = context.Weak || new BodyBindings(context.Body).MayBeSkipped(expression);
        if (new StorageDeferred(context, item => Origin(item, state), state).Value(CallableSyntax.Unwrap(expression), weak) is { } deferred)
            return deferred;
        var evaluation = new StorageEvaluations(context, item => Origin(item, state), state);
        StorageOrigin? Call(InvocationExpressionSyntax call) => evaluation.Invoke(call,
            context.Weak || new BodyBindings(context.Body).MayBeSkipped(call));
        return new StorageOriginResolver(context, state, evaluation.Fresh, Call).Origin(expression);
    }

    private bool Execute(SyntaxNode node, StorageEnvironment state, bool weak)
    {
        if (new StorageReferences(context, expression => Origin(expression, state), state).Bind(node, weak)) return true;
        if (new StorageDeferred(context, expression => Origin(expression, state), state).Execute(node, weak)) return true;
        if (node is InvocationExpressionSyntax call)
        {
            new StorageEvaluations(context, expression => Origin(expression, state), state).Invoke(call, weak);
            return true;
        }
        return node is AssignmentExpressionSyntax assignment
            && new StorageWrites(context, expression => Origin(expression, state), state).Assign(assignment, weak);
    }

}
