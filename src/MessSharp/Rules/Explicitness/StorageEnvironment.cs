namespace MessSharp.Rules.Explicitness;

internal sealed class StorageEnvironment
{
    public Dictionary<string, StorageOrigin> Bindings { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, StorageOrigin> Calls { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, StorageCallable> Callables { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, StorageReference> References { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, StorageDeferredValue> Deferred { get; } = new(StringComparer.Ordinal);
    public StorageHeap Heap { get; set; } = new();

    /// <summary>
    /// Cached call values belong to the frame that computed them. Values from other frames never answer this
    /// frame's queries, yet they would make every path to it look different and defeat the call memo.
    /// This changes the environment in place.
    /// </summary>
    public void ForgetCallsOutside(string scope)
    {
        var prefix = scope + ":";
        foreach (var key in Calls.Keys.Where(key => !key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            Calls.Remove(key);
    }

    public StorageEnvironment Copy()
    {
        var copy = new StorageEnvironment();
        foreach (var pair in Bindings) copy.Bindings[pair.Key] = pair.Value;
        foreach (var pair in Calls) copy.Calls[pair.Key] = pair.Value;
        foreach (var pair in Callables) copy.Callables[pair.Key] = pair.Value;
        foreach (var pair in References) copy.References[pair.Key] = pair.Value;
        foreach (var pair in Deferred) copy.Deferred[pair.Key] = pair.Value;
        copy.Heap = Heap.Copy();
        return copy;
    }
}
