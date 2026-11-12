namespace MessSharp.Rules.Explicitness;

internal static class PrimitiveConversions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Numeric =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["sbyte"] = new HashSet<string> { "short", "int", "long", "float", "double", "decimal" },
            ["byte"] = new HashSet<string> { "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal" },
            ["short"] = new HashSet<string> { "int", "long", "float", "double", "decimal" },
            ["ushort"] = new HashSet<string> { "int", "uint", "long", "ulong", "float", "double", "decimal" },
            ["int"] = new HashSet<string> { "long", "float", "double", "decimal" },
            ["uint"] = new HashSet<string> { "long", "ulong", "float", "double", "decimal" },
            ["long"] = new HashSet<string> { "float", "double", "decimal" },
            ["ulong"] = new HashSet<string> { "float", "double", "decimal" },
            ["char"] = new HashSet<string> { "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal" },
            ["float"] = new HashSet<string> { "double" },
        };

    public static bool CouldConvert(CallableOwner caller, string? source, string target)
    {
        source = source is null ? null : SequenceTypeAliases.Resolve(caller, source);
        target = SequenceTypeAliases.Resolve(caller, target);
        var actual = PrimitiveTypeNames.Name(source);
        var expected = PrimitiveTypeNames.Name(target);
        if (actual is null || expected is null) return true;
        if (source?.EndsWith('?') == true && !target.EndsWith('?')) return false;
        return actual == expected || Numeric.TryGetValue(actual, out var targets) && targets.Contains(expected);
    }

}
