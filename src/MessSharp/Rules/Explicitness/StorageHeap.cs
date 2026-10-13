namespace MessSharp.Rules.Explicitness;

internal sealed class StorageHeap
{
    private readonly Dictionary<string, StorageCell> _cells = new(StringComparer.Ordinal);

    public StorageOrigin Allocate(string identity, StorageCell cell, bool weak = false)
    {
        if (weak && _cells.TryGetValue(identity, out var previous)) previous.Join(cell);
        else _cells[identity] = cell;
        return StorageOrigin.Private with { Objects = new HashSet<string> { identity } };
    }

    public StorageOrigin Shared(string root)
    {
        var origin = StorageOrigin.Shared(root);
        var identity = "external:" + root;
        if (!_cells.ContainsKey(identity)) _cells[identity] = new StorageCell { Default = origin.Element() };
        return origin with { Objects = new HashSet<string> { identity } };
    }

    public StorageOrigin Read(StorageOrigin receiver, string slot)
    {
        if (receiver.Objects.Count == 0) return receiver.Element();
        return Cells(receiver).Select(cell => slot == StorageCell.AnyElement ? Join(cell.Elements) : cell.Read(slot))
            .Aggregate(StorageOrigin.Private, (current, next) => current.Join(next));
    }

    public void Write(StorageOrigin receiver, string slot, StorageOrigin value, bool weak)
    {
        foreach (var cell in Cells(receiver))
        {
            if (slot == StorageCell.AnyElement) { Widen(cell, value); continue; }
            cell.Slots[slot] = weak || receiver.Objects.Count != 1 ? cell.Read(slot).Join(value) : value;
        }
    }

    public void Change(StorageOrigin receiver, StorageMutation mutation, bool weak)
    {
        foreach (var cell in Cells(receiver)) cell.Change(mutation, weak || receiver.Objects.Count != 1);
    }

    public void ReplaceElements(StorageOrigin receiver, StorageOrigin value, bool weak)
    {
        foreach (var cell in Cells(receiver))
        {
            if (weak || receiver.Objects.Count != 1) { Widen(cell, value); continue; }
            foreach (var slot in cell.Slots.Keys.Where(key => !key.StartsWith(".", StringComparison.Ordinal)).ToList())
                cell.Slots.Remove(slot);
            cell.Default = value;
            cell.Length = null;
        }
    }

    public StorageCell CopyElements(StorageOrigin source)
    {
        var cells = Cells(source).ToList();
        if (cells.Count == 0) return new StorageCell { Default = source.Element() };
        var copy = cells[0].Copy();
        foreach (var cell in cells.Skip(1)) copy.Join(cell);
        foreach (var member in copy.Slots.Keys.Where(key => key.StartsWith(".", StringComparison.Ordinal)).ToList())
            copy.Slots.Remove(member);
        copy.Appends.Clear();
        return copy;
    }

    public StorageOrigin Materialize(StorageOrigin origin)
    {
        var reachable = origin.Reachable.ToHashSet();
        Collect(origin, reachable, new HashSet<string>(StringComparer.Ordinal));
        return origin with { Reachable = reachable };
    }

    private void Collect(StorageOrigin origin, HashSet<string> reachable, HashSet<string> visiting)
    {
        foreach (var identity in origin.Objects)
        {
            if (!visiting.Add(identity) || !_cells.TryGetValue(identity, out var cell)) continue;
            foreach (var value in cell.Slots.Values.Append(cell.Default))
            {
                reachable.UnionWith(value.Outer);
                reachable.UnionWith(value.Reachable);
                Collect(value, reachable, visiting);
            }
        }
    }

    public StorageHeap Copy()
    {
        var copy = new StorageHeap();
        foreach (var (identity, cell) in _cells) copy._cells[identity] = cell.Copy();
        return copy;
    }

    public bool Join(StorageHeap other)
    {
        var changed = false;
        foreach (var (identity, cell) in other._cells)
        {
            if (_cells.TryGetValue(identity, out var previous)) changed |= previous.Join(cell);
            else { _cells[identity] = cell.Copy(); changed = true; }
        }
        return changed;
    }

    private IEnumerable<StorageCell> Cells(StorageOrigin origin) => origin.Objects
        .Where(_cells.ContainsKey).Select(identity => _cells[identity]);

    private static StorageOrigin Join(IEnumerable<StorageOrigin> origins) =>
        origins.Aggregate(StorageOrigin.Private, (current, next) => current.Join(next));

    private static void Widen(StorageCell cell, StorageOrigin value)
    {
        cell.Default = cell.Default.Join(value);
        foreach (var slot in cell.Slots.Keys.Where(key => !key.StartsWith(".", StringComparison.Ordinal)).ToList())
            cell.Slots[slot] = cell.Slots[slot].Join(value);
    }
}
