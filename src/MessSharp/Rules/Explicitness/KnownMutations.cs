namespace MessSharp.Rules.Explicitness;

internal static class KnownMutations
{
    private static readonly IReadOnlySet<string> Mutators = new HashSet<string>(StringComparer.Ordinal)
    {
        "Add", "AddRange", "Remove", "RemoveAt", "RemoveRange", "Clear", "Insert", "InsertRange",
        "Sort", "Reverse", "Enqueue", "Dequeue", "Push", "Pop", "TryAdd", "TryRemove", "UnionWith", "ExceptWith",
    };

    public static bool IsMutation(string name) => Mutators.Contains(name);

    public static bool IsCollectionMutation(CallableOwner owner, SyntaxNodeCall call)
    {
        var type = new ExpressionTypes(owner).TypeOf(call.Receiver);
        return type is not null && StorageTypes.IsMutable(type) && !KnownTypeChecks.IsCustomType(owner, type)
            && IsMutation(call.Name);
    }
}
