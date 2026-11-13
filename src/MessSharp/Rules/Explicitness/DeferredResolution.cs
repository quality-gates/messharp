namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The work a deferred value can run. <see cref="Complete"/> is false when some source stays unknown;
/// <see cref="Pending"/> names parameters of the analysed owner whose entry value only a caller knows.
/// </summary>
internal sealed record DeferredResolution(IReadOnlyList<DeferredWork> Work, bool Complete)
{
    public IReadOnlyList<string> Pending { get; init; } = [];

    /// <summary>Collection parameters of the analysed owner whose elements only a caller knows.</summary>
    public IReadOnlyList<string> PendingElements { get; init; } = [];

    public bool Resolved => Complete && Pending.Count == 0 && PendingElements.Count == 0;

    public static DeferredResolution Parameter(string name) => new([], true) { Pending = [name] };

    public static DeferredResolution ElementsOfParameter(string name) => new([], true) { PendingElements = [name] };

    public static DeferredResolution Merge(IEnumerable<DeferredResolution> alternatives)
    {
        var resolved = alternatives.ToList();
        return new DeferredResolution(resolved.SelectMany(value => value.Work).ToList(),
            resolved.Count > 0 && resolved.All(value => value.Complete))
        {
            Pending = resolved.SelectMany(value => value.Pending).Distinct(StringComparer.Ordinal).ToList(),
            PendingElements = resolved.SelectMany(value => value.PendingElements).Distinct(StringComparer.Ordinal).ToList(),
        };
    }
}
