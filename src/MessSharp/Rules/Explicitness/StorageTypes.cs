namespace MessSharp.Rules.Explicitness;

internal static class StorageTypes
{
    private static readonly IReadOnlySet<string> Mutable = new HashSet<string>(StringComparer.Ordinal)
    {
        "List", "Dictionary", "HashSet", "SortedSet", "SortedDictionary", "Queue", "Stack",
        "LinkedList", "Collection", "ObservableCollection", "ConcurrentDictionary", "ConcurrentQueue", "ConcurrentStack",
    };

    public static bool IsMutable(string type)
    {
        if (type.EndsWith("[]", StringComparison.Ordinal)) return true;
        var generic = type.IndexOf('<');
        var name = generic < 0 ? type : type[..generic];
        return Mutable.Contains(name[(name.LastIndexOf('.') + 1)..]);
    }
}
