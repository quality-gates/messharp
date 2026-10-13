using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class KnownCultureProviders
{
    private static readonly IReadOnlySet<string> InvariantFormats = new HashSet<string>(StringComparer.Ordinal)
    {
        "CultureInfo.InvariantCulture", "System.Globalization.CultureInfo.InvariantCulture",
        "NumberFormatInfo.InvariantInfo", "System.Globalization.NumberFormatInfo.InvariantInfo",
    };

    public static bool IsInvariantParse(InvocationExpressionSyntax call) =>
        call.ArgumentList.Arguments.Count == 2
        && InvariantFormats.Contains(call.ArgumentList.Arguments[1].Expression.ToString());
}
