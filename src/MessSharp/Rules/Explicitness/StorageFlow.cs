using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageFlow(StorageContext context)
{
    public StorageEnvironment Before(SyntaxNode site, StorageEnvironment initial, bool after = false) =>
        new BindingOrigins(context.Body, Origin, Execute, context.Weak).Before(site, initial, after);

    public StorageOrigin Origin(ExpressionSyntax expression, StorageEnvironment state)
    {
        var evaluation = new StorageEvaluations(context, item => Origin(item, state), state);
        StorageOrigin? Call(InvocationExpressionSyntax call) => evaluation.Invoke(call,
            context.Weak || new BodyBindings(context.Body).MayBeSkipped(call));
        return new StorageOriginResolver(context, state, evaluation.Fresh, Call).Origin(expression);
    }

    private bool Execute(SyntaxNode node, StorageEnvironment state, bool weak)
    {
        if (node is InvocationExpressionSyntax call)
        {
            new StorageEvaluations(context, expression => Origin(expression, state), state).Invoke(call, weak);
            return true;
        }
        return node is AssignmentExpressionSyntax assignment
            && new StorageWrites(context, expression => Origin(expression, state), state).Assign(assignment, weak);
    }

}
