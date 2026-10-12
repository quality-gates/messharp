using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        if (Actual(invocation.ParameterName) is not { } actual) return [fact];
        if (Callback(actual) is not { } callback) return [fact];
        var inner = new CallEdge(edge.Target, callback, invocation.Call);
        var innerTransfer = new EffectTransfer(ctx, inner, f => [f]);
        var outerTransfer = new EffectTransfer(ctx, edge, f => [f]);
        return summary(callback.Method).Facts
            .SelectMany(f => innerTransfer.Map(f.Key))
            .SelectMany(outerTransfer.Map);
    }

    private ExpressionSyntax? Actual(string name)
    {
        var parameter = edge.Target.Method.Parameters.FindIndex(p => p.Name == name);
        var named = edge.Call.ArgumentList.Arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == name);
        if (named is not null) return named.Expression;
        return parameter >= 0 && parameter < edge.Call.ArgumentList.Arguments.Count
            ? edge.Call.ArgumentList.Arguments[parameter].Expression : null;
    }

    private CallableOwner? Callback(ExpressionSyntax actual)
    {
        actual = CallableSyntax.Unwrap(actual);
        if (actual is IdentifierNameSyntax name)
        {
            if (LocalShadowing.IsShadowedByLocal(name, name.Identifier.Text)) return null;
            return Unique(owners.Where(o => o.Parent is null && o.Method.Class == edge.Caller.Method.Class
                && o.Method.Name == name.Identifier.Text && o.Body is not null));
        }
        return CallableNodeLookup.Find(owners, actual);
    }

    private static CallableOwner? Unique(IEnumerable<CallableOwner> candidates)
    {
        var matches = candidates.Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }
}
