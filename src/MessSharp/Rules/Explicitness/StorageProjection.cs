namespace MessSharp.Rules.Explicitness;

internal static class StorageProjection
{
    public static StorageOrigin Apply(StorageOrigin relative, Func<string, StorageOrigin> actual)
    {
        var result = StorageOrigin.Private;
        foreach (var root in relative.Outer.Union(relative.Reachable))
        {
            var origin = actual(root);
            result = result.Join(!relative.Outer.Contains(root) ? origin.ShallowCopy()
                : relative.Deep.Contains(root) ? origin.Element() : origin);
        }
        return result;
    }
}
