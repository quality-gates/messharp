namespace MessSharp.Rules.Explicitness;

/// <summary>
/// A callable returned out of its factory keeps the factory locals it captures alive across calls, so each such
/// local is closure state rather than storage private to one call.
/// </summary>
internal static class EscapedCaptures
{
    public static DeferredWork Escaping(this DeferredWork work, CallableOwner factory)
    {
        if (work.Kind != DeferredKind.Call || !DeclaredIn(work.Target, factory)) return work;
        var bindings = work.Bindings.ToDictionary(pair => pair.Key,
            pair => IsFrameLocal(factory, pair.Key, pair.Value)
                ? StorageOrigin.Shared(StorageRoots.Capture(pair.Key, factory.Method.Node.SpanStart)) : pair.Value,
            StringComparer.Ordinal);
        return work with { Bindings = bindings };
    }

    private static bool DeclaredIn(CallableOwner target, CallableOwner factory)
    {
        for (var parent = target.Parent; parent is not null; parent = parent.Parent)
            if (parent == factory) return true;
        return false;
    }

    private static bool IsFrameLocal(CallableOwner factory, string name, StorageOrigin storage) =>
        storage.Outer.Count == 0 && storage.Reachable.Count == 0
        && !factory.Method.Parameters.Any(parameter => parameter.Name == name);
}
