using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

internal sealed class CallableEdges
{
    private readonly List<CallEdge> _edges = [];
    private readonly InvocationEdges _invocations;
    private readonly EagerCallbackEdges _callbacks;
    private readonly ExecutionEdges _execution;
    private readonly ConstructorEdges _constructors;
    private readonly ConstructorChainEdges _chains;

    public CallableEdges(RuleContext ctx, IReadOnlyList<CallableOwner> owners)
    {
        _invocations = new InvocationEdges(ctx, owners, _edges);
        _callbacks = new EagerCallbackEdges(ctx, owners, _edges);
        _execution = new ExecutionEdges(ctx, owners, _edges);
        _constructors = new ConstructorEdges(ctx, owners, _edges);
        _chains = new ConstructorChainEdges(ctx, owners, _edges);
    }

    public IReadOnlyList<CallEdge> All => _edges;

    public void Add(CallableOwner owner, EffectSummary summary)
    {
        _invocations.Add(owner, summary);
        _callbacks.Add(owner, summary);
        _execution.Add(owner, summary);
        _constructors.Add(owner, summary);
        _chains.Add(owner, summary);
        UnresolvedExecution.Add(owner, summary);
    }
}
