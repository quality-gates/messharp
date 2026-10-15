namespace MessSharp.Rules.Explicitness;

internal sealed record StorageOrigin(IReadOnlySet<string> Outer, IReadOnlySet<string> Reachable, IReadOnlySet<string> Deep)
{
    public const string UnknownRoot = "?";

    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    public static readonly StorageOrigin Private = new(None, None, None);

    public static readonly StorageOrigin Unknown = Shared(UnknownRoot);

    public IReadOnlySet<string> Outer { get; init; } = Outer;
    public IReadOnlySet<string> Reachable { get; init; } = Reachable;
    public IReadOnlySet<string> Deep { get; init; } = Deep;
    public IReadOnlySet<string> Objects { get; init; } = None;

    public static StorageOrigin Shared(string name) => new(new HashSet<string> { name }, new HashSet<string> { name }, None);

    public static StorageOrigin Holding(StorageOrigin elements) =>
        new(None, elements.Outer.Union(elements.Reachable).ToHashSet(), None);

    public StorageOrigin ShallowCopy() => new(None, Reachable, None);

    public StorageOrigin Element() => new(Reachable, Reachable, Reachable);

    public StorageOrigin Join(StorageOrigin other) => new(Outer.Union(other.Outer).ToHashSet(),
        Reachable.Union(other.Reachable).ToHashSet(), Deep.Union(other.Deep).ToHashSet())
    { Objects = Objects.Union(other.Objects).ToHashSet() };

    public bool SameRoots(StorageOrigin other) => Outer.SetEquals(other.Outer)
        && Reachable.SetEquals(other.Reachable) && Deep.SetEquals(other.Deep) && Objects.SetEquals(other.Objects);
}
