using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class EagerCallbackEdges(RuleContext ctx, IReadOnlyList<CallableOwner> owners, List<CallEdge> edges)
{
    public void Add(CallableOwner owner, EffectSummary summary)
    {
        if (owner.Body is not { } body) return;
        foreach (var call in BodyWalk.Nodes(body).OfType<InvocationExpressionSyntax>())
            if (KnownExecutionOperations.IsEagerCallback(owner, call)) AddCall(owner, summary, call, body);
    }

    /// <summary>A standard operation runs every callable its callback can hold on each element of the receiver.</summary>
    private void AddCall(CallableOwner owner, EffectSummary summary, InvocationExpressionSyntax call, SyntaxNode body)
    {
        var actual = call.ArgumentList.Arguments[0].Expression;
        var member = (MemberAccessExpressionSyntax)call.Expression;
        var argument = CallArguments.Ownership(ctx, owner, owners, body).StorageAt(member.Expression, call).Element();
        var held = new DeferredValueResolver(ctx, owners).Callables(owner, actual, body, 1);
        foreach (var work in held.Work)
        {
            var bindings = work.Captured(call);
            foreach (var parameter in work.Target.OwnParameters()) bindings[parameter] = argument;
            edges.Add(new CallEdge(owner, work.Target, call, call, bindings));
        }
        if (!held.Complete) summary.Add(new EffectFact(EffectKind.Unknown, "unresolved standard callback"), call);
        foreach (var parameter in held.Pending)
            summary.Add(new EffectFact(EffectKind.Unknown, "unresolved standard callback",
                Callback: new CallbackInvocation(parameter, call, [argument])), call);
    }
}
