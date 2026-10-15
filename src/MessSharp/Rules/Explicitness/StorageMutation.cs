namespace MessSharp.Rules.Explicitness;

internal abstract record StorageMutation
{
    public sealed record Append(StorageOrigin Value, string Site) : StorageMutation;
    public sealed record Insert(StorageOrigin Value, int? Index, string Site) : StorageMutation;
    public sealed record Clear : StorageMutation;
    public sealed record Remove(StorageOrigin Value) : StorageMutation;
    public sealed record RemoveRange(int? Index, int? Count) : StorageMutation;
    public sealed record Reverse(int? Index, int? Count, bool Entire) : StorageMutation;
    public sealed record Reorder : StorageMutation;
}
