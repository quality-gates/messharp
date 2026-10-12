namespace MessSharp.Rules.Explicitness;

internal sealed record StorageOrigin(IReadOnlySet<string> Outer, IReadOnlySet<string> Reachable)
{
    public static readonly StorageOrigin Private = new(new HashSet<string>(), new HashSet<string>());

    public static StorageOrigin Shared(string name) => new(new HashSet<string> { name }, new HashSet<string> { name });

    public StorageOrigin ShallowCopy() => new(new HashSet<string>(), Reachable);

    public StorageOrigin Element() => new(Reachable, Reachable);

    public StorageOrigin Join(StorageOrigin other) => new(Outer.Union(other.Outer).ToHashSet(),
        Reachable.Union(other.Reachable).ToHashSet());
}
