using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageCalls(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    public StorageOrigin? Invoke(InvocationExpressionSyntax call, bool weak)
    {
        var frame = StorageCallFrame.Resolve(context, origin, state, call);
        if (frame?.Target.Body is not { } body) return null;
        if (KnownExecutionOperations.IsIterator(frame.Target)) return StorageOrigin.Private;
        return Execute(frame, body, call, call, weak);
    }

    public void Construct(ConstructorCall constructor, SyntaxNode site, StorageOrigin receiver, bool weak)
    {
        if (constructor.Target.Body is { } body)
            Execute(new StorageCallFrame(constructor.Target, receiver), body, constructor.Invocation, site, weak);
    }

    private StorageOrigin Execute(StorageCallFrame frame, SyntaxNode body, InvocationExpressionSyntax call, SyntaxNode site, bool weak)
    {
        var target = frame.Target;
        if (!context.Visiting.Add(target)) return StorageOrigin.Unknown;
        try
        {
            var initial = StorageCallBindings.Bind(context, frame, call, state, origin);
            var flow = new StorageFlow(context.For(target, body, site, weak));
            var final = flow.Before(body, initial, true);
            var returned = StorageOrigin.Private;
            foreach (var expression in Returns(body))
            {
                var atReturn = flow.Before(expression, initial);
                returned = returned.Join(flow.Origin(expression, atReturn));
                final.Heap.Join(atReturn.Heap);
            }
            if (weak) state.Heap.Join(final.Heap);
            else state.Heap = final.Heap.Copy();
            return returned;
        }
        finally { context.Visiting.Remove(target); }
    }

    private static IEnumerable<ExpressionSyntax> Returns(SyntaxNode body) => body is ExpressionSyntax expression
        ? [expression] : BodyWalk.Nodes(body).OfType<ReturnStatementSyntax>().Select(r => r.Expression).OfType<ExpressionSyntax>();
}
