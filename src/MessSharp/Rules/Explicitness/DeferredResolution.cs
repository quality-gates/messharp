namespace MessSharp.Rules.Explicitness;

internal sealed record DeferredResolution(IReadOnlyList<DeferredWork> Work, bool Complete)
{
    public static DeferredResolution Merge(IEnumerable<DeferredResolution> alternatives)
    {
        var resolved = alternatives.ToList();
        return new DeferredResolution(resolved.SelectMany(value => value.Work).ToList(),
            resolved.Count > 0 && resolved.All(value => value.Complete));
    }
}
