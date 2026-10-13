using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

internal enum EffectKind { Input, Output, InstanceInput, InstanceOutput, ParameterChange, ParameterWrite, CaptureRead, CaptureWrite, Unknown }

internal readonly record struct EffectFact(
    EffectKind Kind,
    string What,
    bool Reachable = false,
    CallbackInvocation? Callback = null,
    DeferredExecution? Deferred = null);

internal sealed class EffectSummary
{
    private readonly Dictionary<EffectFact, SyntaxNode> _facts = [];

    public IEnumerable<KeyValuePair<EffectFact, SyntaxNode>> Facts => _facts;

    public bool Add(EffectFact fact, SyntaxNode site) => _facts.TryAdd(fact, site);

    public IEnumerable<Finding> Findings(EffectKind kind) => _facts.Where(f => f.Key.Kind == kind)
        .Select(f => new Finding(f.Value, f.Key.What));

    public IEnumerable<Finding> Outputs(MethodModel method) => Findings(EffectKind.Output)
        .Concat(ParameterFindings(method));

    public IEnumerable<Finding> Inputs() => Findings(EffectKind.Input).Concat(_facts
        .Where(f => f.Key.Kind == EffectKind.CaptureRead)
        .Select(f => new Finding(f.Value, $"reads captured value '{f.Key.What}'")));

    private IEnumerable<Finding> ParameterFindings(MethodModel method)
    {
        foreach (var (fact, site) in _facts)
        {
            if (fact.Kind == EffectKind.ParameterChange)
                yield return new Finding(site, $"changes argument '{fact.What}'");
            if (fact.Kind == EffectKind.CaptureWrite)
                yield return new Finding(site, $"writes captured value '{fact.What}'");
            if (fact.Kind != EffectKind.ParameterWrite) continue;
            var parameter = method.Parameters.FirstOrDefault(p => p.Name == fact.What);
            if (parameter?.WritesReachCaller != true) continue;
            var mode = parameter.IsOut ? "out" : "ref";
            yield return new Finding(site, $"writes {mode} parameter '{fact.What}'");
        }
    }

    public static EffectSummary Direct(RuleContext ctx, CallableOwner owner, IReadOnlyList<CallableOwner> owners)
    {
        var direct = MethodEffectAnalyzer.Analyze(ctx, owner, owners);
        var summary = new EffectSummary();
        summary.AddFindings(EffectKind.Input, direct.AmbientInputs);
        summary.AddFindings(EffectKind.Output, direct.AmbientOutputs);
        foreach (var access in direct.Statics)
        {
            if (access.Kind == AccessKind.Read && !direct.MutableStatics.Contains(access.Name)) continue;
            summary.AddAccess(access.Kind == AccessKind.Read ? EffectKind.Input : EffectKind.Output, access, "static member");
        }
        foreach (var access in direct.Instance)
            summary.AddAccess(access.Kind == AccessKind.Read ? EffectKind.InstanceInput : EffectKind.InstanceOutput, access, "member");
        foreach (var access in direct.Parameters)
            summary.AddParameter(owner, owners, access);
        return summary;
    }

    private void AddParameter(CallableOwner owner, IReadOnlyList<CallableOwner> owners, StateAccess access)
    {
        var captured = CaptureState.Contains(owner, access.Name);
        if (access.Kind == AccessKind.Read)
        {
            if (captured && CaptureState.MayChange(owner, access.Name, owners))
                Add(new EffectFact(EffectKind.CaptureRead, access.Name, access.Deep), access.Site);
            return;
        }
        if (access.Kind == AccessKind.Change)
        {
            Add(StorageRoots.Change(access.Name, access.Deep), access.Site);
            return;
        }
        Add(new EffectFact(captured ? EffectKind.CaptureWrite : EffectKind.ParameterWrite, access.Name), access.Site);
    }

    private void AddFindings(EffectKind kind, IEnumerable<Finding> findings)
    {
        foreach (var finding in findings) Add(new EffectFact(kind, finding.What), finding.Site);
    }

    private void AddAccess(EffectKind kind, StateAccess access, string noun) =>
        Add(new EffectFact(kind, Finding.FromAccess(access, noun).What), access.Site);
}
