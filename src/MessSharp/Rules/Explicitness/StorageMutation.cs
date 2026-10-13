namespace MessSharp.Rules.Explicitness;

internal abstract record StorageMutation
{
    public sealed record Append(StorageOrigin Value, string Site) : StorageMutation;
    public sealed record Insert(StorageOrigin Value, int? Index, string Site) : StorageMutation;
    public sealed record Clear : StorageMutation;
}
