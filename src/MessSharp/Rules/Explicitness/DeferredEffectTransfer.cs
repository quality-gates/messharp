using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class DeferredEffectTransfer(
    RuleContext ctx,
    CallEdge edge,
    IReadOnlyList<CallableOwner> owners,
    Func<MethodModel, EffectSummary> summary)
{
    public IEnumerable<EffectFact> Map(EffectFact fact)
    {
        if (fact.Deferred is not { } execution || new ParameterEntry(ctx, edge, owners).Resolve(execution) is not { } resolved) return [fact];
        var effects = resolved.Work.Where(work => work.Kind == execution.Kind).Select(edge.Through).SelectMany(MapWork).ToList();
        if (!resolved.Resolved) effects.Add(fact with { Deferred = execution.Residual(resolved) });
        return effects;
    }

    private IEnumerable<EffectFact> MapWork(CallEdge executionEdge)
    {
        var nested = new DeferredEffectTransfer(ctx, executionEdge, owners, summary);
        var transfer = new EffectTransfer(ctx, executionEdge, owners,
            fact => (fact.Deferred is null ? [fact] : nested.Map(fact)).Select(item => item with { Deferred = null }));
        return summary(executionEdge.Target.Method).Facts.SelectMany(fact => transfer.Map(fact.Key));
    }
}
