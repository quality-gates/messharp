using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ExecutionEdges(RuleContext ctx, IReadOnlyList<CallableOwner> owners, List<CallEdge> edges)
{
    private readonly DeferredValueResolver _values = new(ctx, owners);

    public void Add(CallableOwner owner, EffectSummary summary)
    {
        if (owner.Body is not { } body) return;
        foreach (var node in BodyWalk.Nodes(body))
        {
            if (node is CommonForEachStatementSyntax loop)
                Force(owner, summary, loop.Expression, loop, DeferredKind.Sequence, body);
            if (node is MemberAccessExpressionSyntax member && member.Name.Identifier.Text == "Value"
                && KnownExecutionOperations.IsLazy(owner, member.Expression))
                Force(owner, summary, member.Expression, member, DeferredKind.Lazy, body);
            if (node is InvocationExpressionSyntax call) AddCall(owner, summary, call, body);
        }
    }

    private void AddCall(CallableOwner owner, EffectSummary summary, InvocationExpressionSyntax call, SyntaxNode body)
    {
        if (KnownExecutionOperations.IsSequenceForce(owner, call))
            Force(owner, summary, ((MemberAccessExpressionSyntax)call.Expression).Expression, call, DeferredKind.Sequence, body);
    }

    private void Force(CallableOwner owner, EffectSummary summary, ExpressionSyntax value, SyntaxNode site, DeferredKind kind,
        SyntaxNode body)
    {
        var work = _values.Resolve(owner, value, body).Where(w => w.Kind == kind).ToList();
        foreach (var item in work) edges.Add(new CallEdge(owner, item.Target, item.Call, site, item.Bindings));
        if (work.Count == 0 && KnownExecutionOperations.RequiresExecution(owner, value, kind))
            summary.Add(new EffectFact(EffectKind.Unknown, "unresolved deferred execution"), site);
    }
}
