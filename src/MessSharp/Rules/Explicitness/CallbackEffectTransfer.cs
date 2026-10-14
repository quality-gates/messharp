using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// A callee's call through a delegate parameter runs, for each caller, every callable the caller's argument can
/// hold. A parameter of the caller is left for the caller's own callers; anything else unresolved stays unknown.
/// </summary>
internal sealed class CallbackEffectTransfer(
    RuleContext ctx,
    CallEdge edge,
    IReadOnlyList<CallableOwner> owners,
    Func<MethodModel, EffectSummary> summary)
{
    public IEnumerable<EffectFact> Map(EffectFact fact)
    {
        if (fact.Callback is not { } invocation) return [fact];
        if (!edge.Target.Method.Parameters.Any(p => p.Name == invocation.ParameterName)) return [fact];
        if (CallArguments.Actual(edge, invocation.ParameterName) is not { } actual
            || edge.Caller.Body is not { } body) return [fact];
        var arguments = invocation.Arguments.Select(argument => CallArguments.Project(ctx, edge, owners, argument, body)).ToList();
        var held = new DeferredValueResolver(ctx, owners).Held(edge.Caller, actual, body, arguments.Count);
        var effects = held.Work.SelectMany(work =>
            CallbackWorkRunner.Run(ctx, owners, summary, work.CallbackEdge(edge, invocation.Call, arguments))).ToList();
        if (!held.Complete) effects.Add(fact with { Callback = null });
        effects.AddRange(held.Pending.Select(parameter =>
            fact with { Callback = new CallbackInvocation(parameter, invocation.Call, arguments) }));
        return effects;
    }
}

internal static class CallbackWorkRunner
{
    public static IEnumerable<EffectFact> Run(RuleContext ctx, IReadOnlyList<CallableOwner> owners,
        Func<MethodModel, EffectSummary> summary, CallEdge callbackEdge)
    {
        var transfer = new EffectTransfer(ctx, callbackEdge, owners, f => [f]);
        return summary(callbackEdge.Target.Method).Facts.SelectMany(f => transfer.Map(f.Key));
    }
}
