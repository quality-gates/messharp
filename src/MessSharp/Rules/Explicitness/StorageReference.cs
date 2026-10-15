namespace MessSharp.Rules.Explicitness;

internal sealed record StorageReference(IReadOnlyDictionary<string, StorageOrigin> Locations)
{
    public static StorageReference From(StorageOrigin receiver, string slot) =>
        new(new Dictionary<string, StorageOrigin>(StringComparer.Ordinal) { [slot] = receiver });

    public StorageOrigin Receiver => Locations.Values.Aggregate(StorageOrigin.Private, (current, next) => current.Join(next));

    public StorageOrigin Read(StorageHeap heap) => Locations.Aggregate(StorageOrigin.Private,
        (current, location) => current.Join(heap.Read(location.Value, location.Key)));

    public void Write(StorageHeap heap, StorageOrigin value, bool weak)
    {
        foreach (var (slot, receiver) in Locations) heap.Write(receiver, slot, value, weak || Locations.Count != 1);
    }

    public StorageReference Join(StorageReference other)
    {
        var locations = Locations.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var (slot, receiver) in other.Locations)
            locations[slot] = locations.GetValueOrDefault(slot, StorageOrigin.Private).Join(receiver);
        return new StorageReference(locations);
    }
}
