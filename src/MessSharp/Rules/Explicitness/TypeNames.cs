namespace MessSharp.Rules.Explicitness;

internal static class TypeNames
{
    /// <summary>The unqualified name of a type, without type arguments or a nullable marker.</summary>
    public static string Simple(string type) => type.Split('<')[0].Split('.').Last().TrimEnd('?');
}
