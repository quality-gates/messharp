using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class ExtensionArgumentTypes
{
    private static readonly IReadOnlyDictionary<string, string> PrimitiveKinds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["string"] = "string",
        ["String"] = "string",
        ["bool"] = "bool",
        ["Boolean"] = "bool",
        ["char"] = "numeric",
        ["Char"] = "numeric",
        ["sbyte"] = "numeric",
        ["SByte"] = "numeric",
        ["byte"] = "numeric",
        ["Byte"] = "numeric",
        ["short"] = "numeric",
        ["Int16"] = "numeric",
        ["ushort"] = "numeric",
        ["UInt16"] = "numeric",
        ["int"] = "numeric",
        ["Int32"] = "numeric",
        ["uint"] = "numeric",
        ["UInt32"] = "numeric",
        ["long"] = "numeric",
        ["Int64"] = "numeric",
        ["ulong"] = "numeric",
        ["UInt64"] = "numeric",
        ["float"] = "numeric",
        ["Single"] = "numeric",
        ["double"] = "numeric",
        ["Double"] = "numeric",
        ["decimal"] = "numeric",
        ["Decimal"] = "numeric",
    };

    public static bool CouldMatch(CallableOwner caller, CallableOwner candidate, InvocationExpressionSyntax call)
    {
        var parameters = candidate.Method.Parameters.Skip(1).ToList();
        var types = new ExpressionTypes(caller);
        var index = 0;
        foreach (var argument in call.ArgumentList.Arguments)
        {
            var parameter = argument.NameColon is { } named
                ? parameters.FirstOrDefault(p => p.Name == named.Name.Identifier.Text)
                : parameters.ElementAtOrDefault(index);
            index++;
            if (parameter is null) continue;
            var expected = PrimitiveKind(parameter.Type);
            var actual = PrimitiveKind(types.TypeOf(argument.Expression));
            if (expected is not null && actual is not null && expected != actual) return false;
        }
        return true;
    }

    public static string? PrimitiveKind(string? type) => type is not null
        && PrimitiveKinds.TryGetValue(TypeNames.Simple(type), out var kind) ? kind : null;
}
