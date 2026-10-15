using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class InvocationEdges(RuleContext ctx, IReadOnlyList<CallableOwner> owners, ICollection<CallEdge> edges)
{
    private readonly CallResolver _resolver = new(owners);
    private readonly DelegateCallEdges _delegates = new(ctx, owners, edges);

    public void Add(CallableOwner owner, EffectSummary summary)
    {
        if (owner.Body is not { } body) return;
        foreach (var call in BodyWalk.Nodes(body).OfType<InvocationExpressionSyntax>())
        {
            var target = _resolver.Resolve(owner, call);
            if (target is not null && !KnownExecutionOperations.IsIterator(target))
                edges.Add(new CallEdge(owner, target, call));
            else if (target is null && !KnownOperations.IsKnown(owner, call))
                _delegates.Add(owner, summary, call, body);
        }
    }
}
