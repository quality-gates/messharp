using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class InvocationEdges(RuleContext ctx, IReadOnlyList<CallableOwner> owners, ICollection<CallEdge> edges)
{
    private readonly CallResolver _resolver = new(owners);

    public void Add(CallableOwner owner, EffectSummary summary)
    {
        if (owner.Body is not { } body) return;
        foreach (var call in BodyWalk.Nodes(body).OfType<InvocationExpressionSyntax>())
        {
            var target = _resolver.Resolve(owner, call);
            if (target is not null && !KnownExecutionOperations.IsIterator(target))
            {
                var edge = new CallEdge(owner, target, call);
                edges.Add(edge with { Bindings = ReturnedCaptureBindings.For(ctx, owners, edge) });
            }
            else if (target is null && !KnownOperations.IsKnown(owner, call))
                summary.Add(new EffectFact(EffectKind.Unknown, "unresolved call '" + call.Expression + "'",
                    Callback: CallbackInvocations.From(ctx, owner, owners, call, body)), call);
        }
    }
}
