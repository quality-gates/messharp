using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Model;

/// <summary>
/// Builds method artifacts for property accessors that contain executable code.
/// </summary>
internal static class PropertyAccessorModelBuilder
{
    internal static IReadOnlyList<MethodModel> Build(
        SourceFile file, ClassModel cls, PropertyDeclarationSyntax node)
    {
        if (node.AccessorList == null) return Array.Empty<MethodModel>();

        return node.AccessorList.Accessors
            .Where(HasExecutableBody)
            .Select(accessor => BuildAccessor(file, cls, node, accessor))
            .ToList();
    }

    private static MethodModel BuildAccessor(
        SourceFile file, ClassModel cls, PropertyDeclarationSyntax property,
        AccessorDeclarationSyntax accessor)
    {
        var span = accessor.SyntaxTree.GetLineSpan(accessor.Span);
        var modifiers = accessor.Modifiers.Count > 0 ? accessor.Modifiers : property.Modifiers;
        var isSetter = accessor.Keyword.IsKind(SyntaxKind.SetKeyword) ||
                       accessor.Keyword.IsKind(SyntaxKind.InitKeyword);
        var propertyType = property.Type.ToString();

        return new MethodModel
        {
            Name = $"{accessor.Keyword.Text}_{property.Identifier.Text}",
            IsConstructor = false,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = ModelBuilderHelpers.IsExported(modifiers),
            IsPrivate = ModelBuilderHelpers.IsPrivate(modifiers),
            Parameters = BuildAccessorParameters(accessor, propertyType),
            ReturnType = isSetter ? "void" : propertyType,
            Class = cls,
            Node = accessor,
            Body = accessor.Body,
            File = file,
        };
    }

    private static bool HasExecutableBody(AccessorDeclarationSyntax accessor) =>
        accessor.Body != null || accessor.ExpressionBody != null;

    private static List<ParameterModel> BuildAccessorParameters(
        AccessorDeclarationSyntax accessor, string propertyType)
    {
        var isSetter = accessor.Keyword.IsKind(SyntaxKind.SetKeyword) ||
                       accessor.Keyword.IsKind(SyntaxKind.InitKeyword);
        if (!isSetter) return new();

        var line = accessor.SyntaxTree.GetLineSpan(accessor.Keyword.Span)
            .StartLinePosition.Line + 1;
        return new()
        {
            new ParameterModel
            {
                Name = "value",
                Type = propertyType,
                Line = line,
                Node = SyntaxFactory.Parameter(SyntaxFactory.Identifier("value")),
            },
        };
    }
}
