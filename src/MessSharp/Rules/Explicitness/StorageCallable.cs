namespace MessSharp.Rules.Explicitness;

internal sealed record StorageCallable(CallableOwner Target, IReadOnlyDictionary<string, StorageOrigin> Captures)
{
    public IReadOnlyDictionary<string, StorageReference> References { get; init; } = new Dictionary<string, StorageReference>();
}
