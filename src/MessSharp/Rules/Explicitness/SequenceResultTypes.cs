using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The type a LINQ-style conversion or projection call returns, from the type of its receiver.</summary>
internal static class SequenceResultTypes
{
    public static string? Find(MemberAccessExpressionSyntax member, Func<ExpressionSyntax, string?> typeOf) =>
        member.Name.Identifier.Text switch
        {
            "ToList" => "List<" + ExpressionTypes.Element(typeOf(member.Expression)) + ">",
            "ToArray" => ExpressionTypes.Element(typeOf(member.Expression)) + "[]",
            "Select" or "Where" => "IEnumerable<" + ExpressionTypes.Element(typeOf(member.Expression)) + ">",
            _ => null,
        };
}
