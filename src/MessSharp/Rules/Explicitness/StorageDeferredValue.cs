namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Where a deferred value is in its life. Created → MaybeForced (forced on some path) → Forced (forced on every path).
/// Created may jump straight to Forced. It only ever moves forward: see <see cref="StorageDeferredValue.AfterForcing"/>.
/// </summary>
internal enum StorageDeferredStatus { Created, MaybeForced, Forced }

internal sealed record StorageDeferredWork(CallableOwner Target, StorageEnvironment Initial, IReadOnlyList<string>? ElementParameters = null)
{
    public StorageDeferredWork WithElements(StorageOrigin element)
    {
        var initial = Initial.Copy();
        foreach (var parameter in ElementParameters ?? []) initial.Bindings[parameter] = element;
        return this with { Initial = initial };
    }
}

internal sealed record StorageDeferredValue(DeferredKind Kind, IReadOnlyList<StorageDeferredWork> Work,
    StorageOrigin? Source = null, bool UsesSource = false, StorageDeferredStatus Status = StorageDeferredStatus.Created)
{
    /// <summary>
    /// The value after one more forcing. A forcing that may not run (<paramref name="weak"/>) leaves a Created value
    /// MaybeForced; one that surely runs makes it Forced. A Forced value stays Forced: a later maybe-forcing must not
    /// take back that it already ran.
    /// </summary>
    public bool IsForced => Status == StorageDeferredStatus.Forced;

    public bool IsMaybeForced => Status == StorageDeferredStatus.MaybeForced;

    public StorageDeferredValue AfterForcing(Certainty weak) => Status switch
    {
        StorageDeferredStatus.Forced => this,
        _ => this with { Status = weak.IsPossible ? StorageDeferredStatus.MaybeForced : StorageDeferredStatus.Forced },
    };
}
