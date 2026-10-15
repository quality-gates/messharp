using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// A call through a delegate runs every callable the delegate can hold at the call. A holder that cannot be
/// resolved is unknown; a delegate parameter is left for each caller to resolve.
/// </summary>
internal sealed class DelegateCallEdges(RuleContext ctx, IReadOnlyList<CallableOwner> owners, ICollection<CallEdge> edges)
{
    public void Add(CallableOwner owner, EffectSummary summary, InvocationExpressionSyntax call, SyntaxNode body)
    {
        var unresolved = "unresolved call '" + call.Expression + "'";
        if (DelegateCallee.Value(call) is not { } callee)
        {
            summary.Add(new EffectFact(EffectKind.Unknown, unresolved), call);
            return;
        }
        var held = new DeferredValueResolver(ctx, owners).Callables(owner, callee, body, call.ArgumentList.Arguments.Count);
        foreach (var work in held.Work)
            edges.Add(new CallEdge(owner, work.Target, call, Bindings: work.Captured(call)));
        if (!held.Complete) summary.Add(new EffectFact(EffectKind.Unknown, unresolved), call);
        if (held.Pending.Count == 0) return;
        var ownership = CallArguments.Ownership(ctx, owner, owners, body);
        var arguments = call.ArgumentList.Arguments.Select(argument => ownership.StorageAt(argument.Expression, call)).ToList();
        foreach (var parameter in held.Pending)
            summary.Add(new EffectFact(EffectKind.Unknown, unresolved,
                Callback: new CallbackInvocation(parameter, call, arguments)), call);
    }
}
