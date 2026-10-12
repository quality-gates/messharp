using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class CallableGraph
{
    private readonly Dictionary<MethodModel, EffectSummary> _summaries = [];
    private readonly List<CallEdge> _edges = [];
    public IReadOnlyList<CallableOwner> Owners { get; }

    private CallableGraph(RuleContext ctx)
    {
        Owners = CallableOwner.Gather(ctx.File);
        var resolver = new CallResolver(Owners);
        foreach (var owner in Owners)
        {
            var summary = EffectSummary.Direct(ctx, owner, Owners);
            _summaries[owner.Method] = summary;
            AddCalls(owner, summary, resolver);
        }
        Propagate(ctx);
    }

    public static CallableGraph For(RuleContext ctx) => ctx.Facts.Get(ctx.File, () => new CallableGraph(ctx));

    public EffectSummary Summary(MethodModel method) => _summaries.GetValueOrDefault(method) ?? new EffectSummary();

    public IEnumerable<MethodModel> ExtraMethods => Owners.Where(o => o.Parent is not null
        || o.Method.Name == "<top-level>").Select(o => o.Method);

    private void AddCalls(CallableOwner owner, EffectSummary summary, CallResolver resolver)
    {
        if (owner.Body is null) return;
        foreach (var call in BodyWalk.Nodes(owner.Body).OfType<InvocationExpressionSyntax>())
        {
            var target = resolver.Resolve(owner, call);
            if (target is not null)
                _edges.Add(new CallEdge(owner, target, call));
            else if (!KnownOperations.IsKnown(owner, call))
                summary.Add(new EffectFact(
                    EffectKind.Unknown,
                    "unresolved call '" + call.Expression + "'",
                    Callback: CallbackInvocation.From(owner, call)), call);
        }
    }

    private void Propagate(RuleContext ctx)
    {
        new CallableEffectPropagator(ctx, Owners, _edges, Summary).Run();
    }
}

internal sealed record CallEdge(CallableOwner Caller, CallableOwner Target, InvocationExpressionSyntax Call);
