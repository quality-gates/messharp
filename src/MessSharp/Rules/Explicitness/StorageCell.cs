namespace MessSharp.Rules.Explicitness;

internal sealed class StorageCell
{
    public const string AnyElement = "[]";

    public StorageOrigin Default { get; set; } = StorageOrigin.Private;
    public int? Length { get; set; }
    public Dictionary<string, StorageOrigin> Slots { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Appends { get; } = new(StringComparer.Ordinal);

    public StorageOrigin Read(string slot) => Slots.GetValueOrDefault(slot, Default);

    public IEnumerable<StorageOrigin> Elements => Slots.Where(pair => !pair.Key.StartsWith(".", StringComparison.Ordinal))
        .Select(pair => pair.Value).Append(Default);

    public StorageCell Copy()
    {
        var copy = new StorageCell { Default = Default, Length = Length };
        foreach (var pair in Slots) copy.Slots[pair.Key] = pair.Value;
        copy.Appends.UnionWith(Appends);
        return copy;
    }

    public bool Join(StorageCell other)
    {
        var changed = JoinDefault(other.Default);
        foreach (var slot in Slots.Keys.Union(other.Slots.Keys).ToList())
        {
            var before = Read(slot);
            var joined = before.Join(other.Read(slot));
            changed |= !before.SameRoots(joined);
            Slots[slot] = joined;
        }
        if (Length != other.Length && Length is not null) { Length = null; changed = true; }
        var count = Appends.Count;
        Appends.UnionWith(other.Appends);
        return changed || count != Appends.Count;
    }

    private bool JoinDefault(StorageOrigin other)
    {
        var joined = Default.Join(other);
        var changed = !Default.SameRoots(joined);
        Default = joined;
        return changed;
    }

    public void Change(StorageMutation mutation, bool weak)
    {
        switch (mutation)
        {
            case StorageMutation.Append append:
                Append(append.Value, append.Site, weak);
                break;
            case StorageMutation.Insert insert:
                Insert(insert.Value, insert.Index, insert.Site, weak);
                break;
            case StorageMutation.Clear when !weak:
                Slots.Clear();
                Default = StorageOrigin.Private;
                Length = 0;
                Appends.Clear();
                break;
        }
    }

    private void Append(StorageOrigin value, string site, bool weak)
    {
        if (weak || !Appends.Add(site)) Length = null;
        if (Length is { } length)
        {
            Slots["[" + length + "]"] = value;
            Length = length + 1;
            return;
        }
        Default = Default.Join(value);
        foreach (var slot in Slots.Keys.Where(key => !key.StartsWith(".", StringComparison.Ordinal)).ToList())
            Slots[slot] = Slots[slot].Join(value);
    }

    private void Insert(StorageOrigin value, int? index, string site, bool weak)
    {
        if (InsertionPosition(index) is not { } position || !TracksInsert(site, weak))
        { Append(value, site, true); return; }
        for (var at = position.Length; at > position.Index; at--) Slots["[" + at + "]"] = Read("[" + (at - 1) + "]");
        Slots["[" + position.Index + "]"] = value;
        Length = position.Length + 1;
    }

    private (int Length, int Index)? InsertionPosition(int? index)
    {
        if (Length is not { } length || index is not { } position) return null;
        return position < 0 || position > length ? null : (length, position);
    }

    private bool TracksInsert(string site, bool weak) => !weak && Appends.Add(site);
}
