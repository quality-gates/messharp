using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

internal sealed class DeferredEffectTransfer(
    RuleContext ctx,
    CallEdge edge,
    IReadOnlyList<CallableOwner> owners,
    Func<MethodModel, EffectSummary> summary)
{
    public IEnumerable<EffectFact> Map(EffectFact fact)
    {
        if (fact.Deferred is not { } execution
            || !edge.Target.Method.Parameters.Any(parameter => parameter.Name == execution.ParameterName)
            || CallArguments.Actual(edge, execution.ParameterName) is not { } actual
            || edge.Caller.Body is not { } body) return [fact];
        var resolved = new DeferredValueResolver(ctx, owners).Resolve(edge.Caller, actual, body, execution.Kind);
        var effects = resolved.Work.Where(work => work.Kind == execution.Kind).SelectMany(MapWork).ToList();
        if (!resolved.Complete)
            effects.Add(fact with { Deferred = DeferredExecution.From(edge.Caller, actual, execution.Kind, body) });
        return effects;
    }

    private IEnumerable<EffectFact> MapWork(DeferredWork work)
    {
        var executionEdge = new CallEdge(edge.Caller, work.Target, work.Call, edge.Site, work.Bindings);
        var transfer = new EffectTransfer(ctx, executionEdge, owners, fact => [fact with { Deferred = null }]);
        return summary(work.Target.Method).Facts.SelectMany(fact => transfer.Map(fact.Key));
    }
}
