namespace MessSharp;

/// <summary>
/// The things a walk has already seen. A thing is either unseen or seen, and it never goes back: <see cref="TryVisit"/>
/// is true only the first time, which is how a walk over a graph with cycles or shared parts stops.
/// </summary>
internal sealed class Visited<T>(IEqualityComparer<T>? comparer = null) where T : notnull
{
    private readonly HashSet<T> _seen = new(comparer);

    /// <summary>Marks <paramref name="item"/> seen; true if it was not seen before.</summary>
    public bool TryVisit(T item) => _seen.Add(item);
}
