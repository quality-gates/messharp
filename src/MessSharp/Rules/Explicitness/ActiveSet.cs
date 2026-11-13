namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The things being worked on right now. A thing is either idle or active; <see cref="Enter"/> moves it to active and
/// disposing the returned scope moves it back, so no caller has to pair an add with a remove by hand. A thing that is
/// already active cannot be entered again, which is how recursion is cut off.
/// </summary>
internal sealed class ActiveSet<T>(IEqualityComparer<T>? comparer = null) where T : notnull
{
    private readonly HashSet<T> _active = new(comparer);

    public bool IsActive(T item) => _active.Contains(item);

    public IEnumerable<T> Items => _active;

    /// <summary>Marks <paramref name="item"/> active. Null when it already was, and nothing changes.</summary>
    public Scope? Enter(T item) => _active.Add(item) ? new Scope(_active, item) : null;

    /// <summary>Holds one item active until disposed. Only the scope that made the item active can make it idle.</summary>
    public sealed class Scope(HashSet<T> active, T item) : IDisposable
    {
        public void Dispose() => active.Remove(item);
    }
}
