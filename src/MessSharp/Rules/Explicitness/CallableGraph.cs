using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class CallableGraph
{
    private readonly Dictionary<MethodModel, EffectSummary> _summaries = [];
    private readonly CallableEdges _edges;
    public IReadOnlyList<CallableOwner> Owners { get; }

    private CallableGraph(RuleContext ctx)
    {
        Owners = CallableOwner.Gather(ctx.File);
        _edges = new CallableEdges(ctx, Owners);
        foreach (var owner in Owners)
        {
            var summary = EffectSummary.Direct(ctx, owner, Owners);
            _summaries[owner.Method] = summary;
            _edges.Add(owner, summary);
        }
        Propagate(ctx);
    }

    public static CallableGraph For(RuleContext ctx) => ctx.Facts.Get(ctx.File, () => new CallableGraph(ctx));

    public EffectSummary Summary(MethodModel method) => _summaries.GetValueOrDefault(method) ?? new EffectSummary();

    public IEnumerable<MethodModel> ExtraMethods => Owners.Where(o => o.Parent is not null
        || o.Method.Name is "<top-level>" or "<module>").Select(o => o.Method);

    private void Propagate(RuleContext ctx)
    {
        new CallableEffectPropagator(ctx, Owners, _edges.All, Summary).Run();
    }
}

internal sealed record CallEdge(CallableOwner Caller, CallableOwner Target, InvocationExpressionSyntax Call,
    SyntaxNode? ExecutionSite = null, IReadOnlyDictionary<string, StorageOrigin>? Bindings = null)
{
    public SyntaxNode Site => ExecutionSite ?? Call;
}
