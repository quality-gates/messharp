namespace MessSharp.Rules.Explicitness;

/// <summary>A deferred value held by a parameter, or by the elements of a collection parameter, that each caller resolves.</summary>
internal sealed record DeferredExecution(string ParameterName, DeferredKind Kind, bool Elements = false)
{
    /// <summary>What remains deferred of this execution once a caller has resolved part of it.</summary>
    public DeferredExecution? Residual(DeferredResolution resolved) => From(resolved, Kind);

    /// <summary>
    /// A Lazy whose only unknown source is one parameter's entry value, or the elements of one collection
    /// parameter: each caller resolves its own argument.
    /// </summary>
    public static DeferredExecution? From(DeferredResolution resolution, DeferredKind kind)
    {
        if (kind != DeferredKind.Lazy || !resolution.Complete) return null;
        return (resolution.Pending, resolution.PendingElements) switch
        {
            ([var parameter], []) => new DeferredExecution(parameter, kind),
            ([], [var collection]) => new DeferredExecution(collection, kind, Elements: true),
            _ => null,
        };
    }
}
