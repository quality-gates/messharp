namespace MessSharp.Rules.Explicitness;

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
    StorageOrigin? Source = null, bool UsesSource = false, StorageDeferredStatus Status = StorageDeferredStatus.Created);
