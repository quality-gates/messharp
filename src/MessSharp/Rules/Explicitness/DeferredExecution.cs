namespace MessSharp.Rules.Explicitness;

internal sealed record DeferredExecution(string ParameterName, DeferredKind Kind)
{
    /// <summary>
    /// A Lazy whose only unknown source is one parameter's entry value: each caller resolves its own argument.
    /// </summary>
    public static DeferredExecution? From(DeferredResolution resolution, DeferredKind kind) =>
        kind == DeferredKind.Lazy && resolution is { Complete: true, Pending.Count: 1 }
            ? new DeferredExecution(resolution.Pending[0], kind) : null;
}
