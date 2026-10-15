using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class KnownParseOperations
{
    private static readonly IReadOnlySet<SyntaxKind> Numeric = new HashSet<SyntaxKind>
    {
        SyntaxKind.IntKeyword, SyntaxKind.LongKeyword, SyntaxKind.ShortKeyword, SyntaxKind.ByteKeyword,
        SyntaxKind.SByteKeyword, SyntaxKind.UIntKeyword, SyntaxKind.ULongKeyword, SyntaxKind.UShortKeyword,
        SyntaxKind.FloatKeyword, SyntaxKind.DoubleKeyword, SyntaxKind.DecimalKeyword,
    };

    public static bool IsKnown(InvocationExpressionSyntax call) => Keyword(call) switch
    {
        null => false,
        SyntaxKind.BoolKeyword or SyntaxKind.CharKeyword => call.ArgumentList.Arguments.Count == 1,
        _ => call.ArgumentList.Arguments.Count == 1 || KnownCultureProviders.IsInvariantParse(call),
    };

    public static bool ReadsCulture(InvocationExpressionSyntax call) =>
        Keyword(call) is { } keyword && Numeric.Contains(keyword) && call.ArgumentList.Arguments.Count == 1;

    private static SyntaxKind? Keyword(InvocationExpressionSyntax call)
    {
        if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "Parse" } member) return null;
        if (member.Expression is not PredefinedTypeSyntax predefined) return null;
        var keyword = predefined.Keyword.Kind();
        var parses = Numeric.Contains(keyword) || keyword is SyntaxKind.BoolKeyword or SyntaxKind.CharKeyword;
        return parses ? keyword : null;
    }

}
