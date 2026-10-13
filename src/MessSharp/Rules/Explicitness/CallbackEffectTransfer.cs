using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

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
        var body = edge.Caller.Body;
        if (CallArguments.Actual(edge, invocation.ParameterName) is not { } actual
            || body is null) return [fact];
        var arguments = invocation.Arguments.Select(argument => CallArguments.Project(ctx, edge, owners, argument, body)).ToList();
        if (new CallbackResolver(owners).Find(edge.Caller, actual) is not { } callback)
            return CallbackInvocations.Forward(fact, invocation, actual, arguments);
        var bindings = callback.Method.Parameters.Take(arguments.Count)
            .Select((parameter, index) => (parameter.Name, Origin: arguments[index]))
            .ToDictionary(pair => pair.Name, pair => pair.Origin, StringComparer.Ordinal);
        var callbackEdge = new CallEdge(edge.Caller, callback, invocation.Call, edge.Site, bindings);
        var transfer = new EffectTransfer(ctx, callbackEdge, owners, f => [f]);
        return summary(callback.Method).Facts.SelectMany(f => transfer.Map(f.Key));
    }

}
