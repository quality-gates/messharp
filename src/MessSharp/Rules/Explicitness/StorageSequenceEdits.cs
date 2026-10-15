namespace MessSharp.Rules.Explicitness;

internal sealed class StorageSequenceEdits(StorageCell cell)
{
    public void Apply(StorageMutation mutation, bool weak)
    {
        if (weak) { Widen(); return; }
        switch (mutation)
        {
            case StorageMutation.Remove remove:
                Remove(remove.Value);
                break;
            case StorageMutation.RemoveRange range:
                RemoveRange(range.Index, range.Count);
                break;
            case StorageMutation.Reverse reverse:
                Reverse(reverse);
                break;
            default:
                Widen();
                break;
        }
    }

    private void Remove(StorageOrigin value)
    {
        if (cell.Length is not { } length || value.Objects.Count != 1) { Widen(); return; }
        for (var index = 0; index < length; index++)
        {
            if (!cell.Read(Slot(index)).SameRoots(value)) continue;
            RemoveRange(index, 1);
            return;
        }
        Widen();
    }

    private void RemoveRange(int? index, int? count)
    {
        if (Bounds(index, count) is not { } range) { Widen(); return; }
        var length = cell.Length!.Value;
        var removed = range.Count;
        for (var at = range.Index; at < length - removed; at++)
            cell.Slots[Slot(at)] = cell.Read(Slot(at + removed));
        for (var at = length - removed; at < length; at++) cell.Slots.Remove(Slot(at));
        cell.Length = length - removed;
    }

    private void Reverse(StorageMutation.Reverse reverse)
    {
        var count = reverse.Entire ? cell.Length : reverse.Count;
        if (Bounds(reverse.Index, count) is not { } range) { Widen(); return; }
        var size = range.Count;
        for (var at = 0; at < size / 2; at++)
        {
            var left = Slot(range.Index + at);
            var right = Slot(range.Index + size - at - 1);
            var value = cell.Read(left);
            cell.Slots[left] = cell.Read(right);
            cell.Slots[right] = value;
        }
    }

    private (int Index, int Count)? Bounds(int? index, int? count)
    {
        if (cell.Length is not { } length || index is not { } start || count is not { } size) return null;
        return ValidRange(start, size, length) ? (start, size) : null;
    }

    private static bool ValidRange(int start, int size, int length) =>
        start >= 0 && size >= 0 && start <= length && size <= length - start;

    private void Widen()
    {
        var value = cell.Elements.Aggregate(StorageOrigin.Private, (current, next) => current.Join(next));
        cell.Default = value;
        foreach (var slot in cell.Slots.Keys.Where(key => !key.StartsWith(".", StringComparison.Ordinal)).ToList())
            cell.Slots[slot] = value;
        cell.Length = null;
    }

    private static string Slot(int index) => "[" + index + "]";
}
