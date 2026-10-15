using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>A subscription runs the event's accessor at the assignment, with <c>value</c> bound to the handler.</summary>
internal sealed class EventSubscriptionEdges(IReadOnlyList<CallableOwner> owners, ICollection<CallEdge> edges)
{
    private readonly EventSubscriptions _subscriptions = new(owners);

    public void Add(CallableOwner owner, EffectSummary summary)
    {
        if (owner.Body is not { } body) return;
        foreach (var subscription in _subscriptions.Find(owner, body))
        {
            var assignment = subscription.Assignment;
            if (subscription.Accessor is not { } accessor)
            {
                summary.Add(new EffectFact(EffectKind.Unknown, "unresolved event accessor '" + subscription.AccessorName + "'"), assignment);
                continue;
            }
            var call = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(accessor.Method.Name));
            edges.Add(new CallEdge(owner, accessor, call, assignment,
                Actuals: new Dictionary<string, ExpressionSyntax> { ["value"] = assignment.Right }));
        }
    }
}
