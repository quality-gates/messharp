namespace MessSharp.Rules.Explicitness;

internal static class PrimitiveTypeNames
{
    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["string"] = "string",
        ["String"] = "string",
        ["bool"] = "bool",
        ["Boolean"] = "bool",
        ["char"] = "char",
        ["Char"] = "char",
        ["sbyte"] = "sbyte",
        ["SByte"] = "sbyte",
        ["byte"] = "byte",
        ["Byte"] = "byte",
        ["short"] = "short",
        ["Int16"] = "short",
        ["ushort"] = "ushort",
        ["UInt16"] = "ushort",
        ["int"] = "int",
        ["Int32"] = "int",
        ["uint"] = "uint",
        ["UInt32"] = "uint",
        ["long"] = "long",
        ["Int64"] = "long",
        ["ulong"] = "ulong",
        ["UInt64"] = "ulong",
        ["float"] = "float",
        ["Single"] = "float",
        ["double"] = "double",
        ["Double"] = "double",
        ["decimal"] = "decimal",
        ["Decimal"] = "decimal",
    };

    public static string? Name(string? type) => type is not null
        && Names.TryGetValue(TypeNames.Simple(type), out var name) ? name : null;
}
