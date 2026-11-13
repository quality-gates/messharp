namespace MessSharp.Rules.Explicitness;

internal sealed class StorageHeap
{
    private readonly Dictionary<string, StorageCell> _cells = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, StorageCell> Contents => _cells;

    public StorageOrigin Allocate(string identity, StorageCell cell, Certainty weak = default)
    {
        if (weak.IsPossible && _cells.TryGetValue(identity, out var previous)) previous.Join(cell);
        else _cells[identity] = cell;
        return StorageOrigin.Object(identity);
    }

    public StorageOrigin Shared(string root)
    {
        var origin = StorageOrigin.Shared(root);
        var identity = "external:" + root;
        if (!_cells.ContainsKey(identity)) _cells[identity] = new StorageCell { Default = origin.Element() };
        return origin with { Objects = new HashSet<string> { identity } };
    }

    /// <summary>What <paramref name="slot"/> of the objects at <paramref name="receiver"/> may hold; with no slot, what any element may hold.</summary>
    public StorageOrigin Read(StorageOrigin receiver, string slot = StorageCell.AnyElement)
    {
        if (receiver.Objects.Count == 0) return receiver.Element();
        return Cells(receiver).Select(cell => slot == StorageCell.AnyElement ? Join(cell.Elements) : cell.Read(slot))
            .Aggregate(StorageOrigin.Private, (current, next) => current.Join(next));
    }

    public void Write(StorageOrigin receiver, string slot, StorageOrigin value, Certainty weak)
    {
        foreach (var cell in Cells(receiver))
        {
            if (slot == StorageCell.AnyElement) { Widen(cell, value); continue; }
            cell.Slots[slot] = weak.OrIf(receiver.Objects.Count != 1).IsPossible ? cell.Read(slot).Join(value) : value;
        }
    }

    public void Change(StorageOrigin receiver, StorageMutation mutation, Certainty weak)
    {
        foreach (var cell in Cells(receiver)) cell.Change(mutation, weak.OrIf(receiver.Objects.Count != 1));
    }

    public void ReplaceElements(StorageOrigin receiver, StorageOrigin value, Certainty weak)
    {
        foreach (var cell in Cells(receiver))
        {
            if (weak.IsPossible || receiver.Objects.Count != 1) { Widen(cell, value); continue; }
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
        Collect(origin, reachable, new Visited<string>(StringComparer.Ordinal));
        return origin with { Reachable = reachable };
    }

    private void Collect(StorageOrigin origin, HashSet<string> reachable, Visited<string> visited)
    {
        foreach (var identity in origin.Objects)
        {
            if (!visited.TryVisit(identity) || !_cells.TryGetValue(identity, out var cell)) continue;
            foreach (var value in cell.Slots.Values.Append(cell.Default))
            {
                reachable.UnionWith(value.Outer);
                reachable.UnionWith(value.Reachable);
                Collect(value, reachable, visited);
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
