using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

internal sealed class EffectTransfer(
    RuleContext ctx,
    CallEdge edge,
    IReadOnlyList<CallableOwner> owners,
    Func<EffectFact, IEnumerable<EffectFact>> mapUnknown)
{
    private static readonly IReadOnlySet<EffectKind> RelativeKinds = new HashSet<EffectKind>
        { EffectKind.ParameterChange, EffectKind.ParameterWrite, EffectKind.CaptureRead, EffectKind.CaptureWrite };

    public IEnumerable<EffectFact> Map(EffectFact fact)
    {
        if (fact.Kind == EffectKind.Unknown) return mapUnknown(fact);
        if (!RelativeKinds.Contains(fact.Kind)) return [fact];
        return MapParameterEffect(fact);
    }

    private IEnumerable<EffectFact> MapParameterEffect(EffectFact fact)
    {
        var targetParameter = edge.Target.Method.Parameters.FirstOrDefault(p => p.Name == fact.What);
        if (fact.Kind == EffectKind.ParameterWrite && targetParameter?.WritesReachCaller != true) return [];
        if (edge.Caller.Body is not { } body) return [];
        if (fact.Kind == EffectKind.ParameterWrite && CallArguments.Location(ctx, edge, owners, fact.What, body) is { } holder)
            return MapStorage(fact with { Kind = EffectKind.ParameterChange }, holder);
        return MapStorage(fact, CallArguments.Storage(ctx, edge, owners, fact.What, body));
    }

    private IEnumerable<EffectFact> MapStorage(EffectFact fact, StorageOrigin storage)
    {
        var origins = fact.Reachable ? storage.Reachable : storage.Outer;
        return origins.SelectMany(root => MapRoot(fact, root, storage.Deep.Contains(root)));
    }

    private IEnumerable<EffectFact> MapRoot(EffectFact fact, string root, bool deep)
    {
        if (root == StorageOrigin.UnknownRoot) return [new EffectFact(EffectKind.Unknown, "unresolved storage binding")];
        if (fact.Kind == EffectKind.ParameterChange) return [StorageRoots.Change(root, fact.Reachable || deep)];
        return fact.Kind == EffectKind.CaptureRead ? MapCaptureRead(fact, root, deep) : [MapWrite(root, deep)];
    }

    private IEnumerable<EffectFact> MapCaptureRead(EffectFact fact, string root, bool deep) =>
        CaptureState.Contains(edge.Caller, root) ? [fact with { What = root, Reachable = deep }]
            : StorageRoots.IsExternal(root) ? [StorageRoots.Read(root)] : [];

    private EffectFact MapWrite(string root, bool deep)
    {
        if (StorageRoots.IsExternal(root)) return deep ? StorageRoots.Change(root, true) : StorageRoots.Write(root);
        var kind = CaptureState.Contains(edge.Caller, root) ? EffectKind.CaptureWrite : EffectKind.ParameterWrite;
        return new EffectFact(kind, root, deep);
    }
}
