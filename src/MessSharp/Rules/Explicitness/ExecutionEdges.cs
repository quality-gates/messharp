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
            if (LazyReceiver(node) is { } receiver && KnownExecutionOperations.IsLazyValue(owner, receiver))
                Force(owner, summary, receiver, node, DeferredKind.Lazy, body);
            if (node is InvocationExpressionSyntax call) AddCall(owner, summary, call, body);
        }
    }

    private static ExpressionSyntax? LazyReceiver(SyntaxNode node) => node switch
    {
        MemberAccessExpressionSyntax member when member.Name.Identifier.Text == "Value" => member.Expression,
        MemberBindingExpressionSyntax member when member.Name.Identifier.Text == "Value" =>
            member.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression,
        _ => null,
    };

    private void AddCall(CallableOwner owner, EffectSummary summary, InvocationExpressionSyntax call, SyntaxNode body)
    {
        if (KnownExecutionOperations.IsSequenceForce(owner, call))
            Force(owner, summary, ((MemberAccessExpressionSyntax)call.Expression).Expression, call, DeferredKind.Sequence, body);
    }

    private void Force(CallableOwner owner, EffectSummary summary, ExpressionSyntax value, SyntaxNode site, DeferredKind kind,
        SyntaxNode body)
    {
        var resolution = _values.Resolve(owner, value, body, kind);
        foreach (var item in resolution.Work.Where(work => work.Kind == kind))
            edges.Add(new CallEdge(owner, item.Target, item.Call, site, item.Bindings));
        if (!resolution.Resolved && KnownExecutionOperations.RequiresExecution(owner, value, kind))
            summary.Add(new EffectFact(EffectKind.Unknown, "unresolved deferred execution",
                Deferred: DeferredExecution.From(resolution, kind)), site);
    }
}
