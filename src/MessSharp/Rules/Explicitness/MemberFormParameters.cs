using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The parameters of a member-form owner: an indexer's index list and an accessor's implicit <c>value</c>.</summary>
internal static class MemberFormParameters
{
    public static List<ParameterModel> Declared(BaseParameterListSyntax list) => list.Parameters.Select(parameter =>
        new ParameterModel
        {
            Name = parameter.Identifier.Text,
            Type = parameter.Type?.ToString() ?? "",
            IsOut = parameter.Modifiers.Any(SyntaxKind.OutKeyword),
            IsRef = parameter.Modifiers.Any(SyntaxKind.RefKeyword),
            Node = parameter,
        }).ToList();

    public static ParameterModel Value(TypeSyntax type, AccessorDeclarationSyntax accessor) => new()
    {
        Name = "value",
        Type = type.ToString(),
        Node = AccessorValue.Parameter(accessor),
    };
}
