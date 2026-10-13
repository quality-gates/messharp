using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

internal sealed class CallableEffectPropagator(
    RuleContext ctx,
    IReadOnlyList<CallableOwner> owners,
    IReadOnlyList<CallEdge> edges,
    Func<MethodModel, EffectSummary> summary)
{
    public void Run()
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var edge in edges)
                changed |= Propagate(edge);
        } while (changed);
    }

    private bool Propagate(CallEdge edge)
    {
        var changed = false;
        var callbacks = new CallbackEffectTransfer(ctx, edge, owners, summary);
        var transfer = new EffectTransfer(ctx, edge, owners, callbacks.Map);
        foreach (var (fact, _) in summary(edge.Target.Method).Facts.ToList())
            foreach (var mapped in transfer.Map(fact))
                changed |= summary(edge.Caller.Method).Add(mapped, edge.Site);
        return changed;
    }
}
