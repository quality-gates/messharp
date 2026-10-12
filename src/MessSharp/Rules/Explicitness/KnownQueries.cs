namespace MessSharp.Rules.Explicitness;

internal static class KnownQueries
{
    private static readonly IReadOnlySet<string> CollectionQueries = new HashSet<string>(StringComparer.Ordinal)
    {
        "Contains", "IndexOf", "LastIndexOf", "Count", "Sum", "Min", "Max", "Any", "All",
        "Clone", "ToArray", "ToList", "AsReadOnly", "TryGetValue", "BinarySearch",
    };

    public static bool IsCollectionQuery(string name) => CollectionQueries.Contains(name);

    public static bool IsStringCalculation(string type, string name) =>
        type is "string" or "System.String" && name is "Contains" or "StartsWith" or "EndsWith"
            or "Substring" or "Trim" or "ToUpperInvariant" or "ToLowerInvariant";
}
