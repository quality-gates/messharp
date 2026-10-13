namespace MessSharp.Rules.Explicitness;

internal static class StorageRoots
{
    private const string StaticPrefix = "static:";
    private const string MemberPrefix = "member:";
    private const string CapturePrefix = "capture:";

    public static string Static(string name) => StaticPrefix + name;

    public static string Member(string name) => MemberPrefix + name;

    public static string Capture(string name, int owner) => CapturePrefix + owner + ":" + name;

    public static bool IsExternal(string root) => root == StorageOrigin.UnknownRoot
        || root.StartsWith(StaticPrefix, StringComparison.Ordinal) || root.StartsWith(MemberPrefix, StringComparison.Ordinal)
        || root.StartsWith(CapturePrefix, StringComparison.Ordinal);

    public static EffectFact Read(string root) => root switch
    {
        StorageOrigin.UnknownRoot => new EffectFact(EffectKind.Unknown, "reads storage of unknown origin"),
        _ when root.StartsWith(StaticPrefix, StringComparison.Ordinal) =>
            new EffectFact(EffectKind.Input, $"reads static member '{root[StaticPrefix.Length..]}'"),
        _ when root.StartsWith(MemberPrefix, StringComparison.Ordinal) =>
            new EffectFact(EffectKind.InstanceInput, $"reads member '{root[MemberPrefix.Length..]}'"),
        _ when root.StartsWith(CapturePrefix, StringComparison.Ordinal) =>
            new EffectFact(EffectKind.Input, $"reads captured value '{root[(root.LastIndexOf(':') + 1)..]}'"),
        _ => new EffectFact(EffectKind.CaptureRead, root),
    };

    public static EffectFact Write(string root) => root switch
    {
        _ when root.StartsWith(StaticPrefix, StringComparison.Ordinal) =>
            new EffectFact(EffectKind.Output, $"writes static member '{root[StaticPrefix.Length..]}'"),
        _ when root.StartsWith(MemberPrefix, StringComparison.Ordinal) =>
            new EffectFact(EffectKind.InstanceOutput, $"writes member '{root[MemberPrefix.Length..]}'"),
        _ when root.StartsWith(CapturePrefix, StringComparison.Ordinal) =>
            new EffectFact(EffectKind.Output, $"writes captured value '{root[(root.LastIndexOf(':') + 1)..]}'"),
        _ => new EffectFact(EffectKind.ParameterWrite, root),
    };

    public static string? Preferred(IEnumerable<string> roots) =>
        roots.OrderBy(Rank).ThenBy(root => root, StringComparer.Ordinal).FirstOrDefault();

    private static int Rank(string root)
    {
        if (root == StorageOrigin.UnknownRoot) return 3;
        if (root.StartsWith(StaticPrefix, StringComparison.Ordinal)) return 1;
        return root.StartsWith(MemberPrefix, StringComparison.Ordinal) ? 2 : 0;
    }

    public static EffectFact Change(string root, bool reachable) => root switch
    {
        StorageOrigin.UnknownRoot => new EffectFact(EffectKind.Unknown, "changes storage of unknown origin"),
        _ when root.StartsWith(StaticPrefix, StringComparison.Ordinal) =>
            new EffectFact(EffectKind.Output, $"changes the object in static member '{root[StaticPrefix.Length..]}'"),
        _ when root.StartsWith(MemberPrefix, StringComparison.Ordinal) =>
            new EffectFact(EffectKind.InstanceOutput, $"changes the object in member '{root[MemberPrefix.Length..]}'"),
        _ when root.StartsWith(CapturePrefix, StringComparison.Ordinal) =>
            new EffectFact(EffectKind.Output, $"changes captured storage '{root[(root.LastIndexOf(':') + 1)..]}'"),
        _ => new EffectFact(EffectKind.ParameterChange, root, reachable),
    };
}
