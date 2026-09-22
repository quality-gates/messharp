using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Model;

/// <summary>
/// Builds method artifacts for executable class members.
/// </summary>
internal static class MethodModelBuilder
{
    internal static IReadOnlyList<MethodModel> Build(
        SourceFile file, ClassModel cls, MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax m => new[] { BuildMethod(file, cls, m) },
            ConstructorDeclarationSyntax c => new[] { BuildConstructor(file, cls, c) },
            OperatorDeclarationSyntax o => new[] { BuildOperator(file, cls, o) },
            ConversionOperatorDeclarationSyntax co => new[] { BuildConversionOperator(file, cls, co) },
            DestructorDeclarationSyntax d => new[] { BuildDestructor(file, cls, d) },
            PropertyDeclarationSyntax p => PropertyAccessorModelBuilder.Build(file, cls, p),
            _ => Array.Empty<MethodModel>(),
        };
    }

    private static MethodModel BuildMethod(SourceFile file, ClassModel cls, MethodDeclarationSyntax node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new MethodModel
        {
            Name = node.Identifier.Text,
            IsConstructor = false,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = ModelBuilderHelpers.IsExported(node.Modifiers),
            IsPrivate = ModelBuilderHelpers.IsPrivate(node.Modifiers),
            IsExplicitInterfaceImplementation = node.ExplicitInterfaceSpecifier is not null,
            Parameters = ModelBuilderHelpers.BuildParameters(node.ParameterList),
            ReturnType = node.ReturnType.ToString(),
            Class = cls,
            Node = node,
            Body = node.Body,
            File = file,
        };
    }

    private static MethodModel BuildConstructor(SourceFile file, ClassModel cls, ConstructorDeclarationSyntax node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new MethodModel
        {
            Name = node.Identifier.Text,
            IsConstructor = true,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = ModelBuilderHelpers.IsExported(node.Modifiers),
            IsPrivate = ModelBuilderHelpers.IsPrivate(node.Modifiers),
            Parameters = ModelBuilderHelpers.BuildParameters(node.ParameterList),
            ReturnType = "",
            Class = cls,
            Node = node,
            Body = node.Body,
            File = file,
        };
    }

    private static MethodModel BuildOperator(SourceFile file, ClassModel cls, OperatorDeclarationSyntax node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new MethodModel
        {
            Name = $"operator {node.OperatorToken.Text}",
            IsConstructor = false,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = ModelBuilderHelpers.IsExported(node.Modifiers),
            IsPrivate = ModelBuilderHelpers.IsPrivate(node.Modifiers),
            Parameters = ModelBuilderHelpers.BuildParameters(node.ParameterList),
            ReturnType = node.ReturnType.ToString(),
            Class = cls,
            Node = node,
            Body = node.Body,
            File = file,
        };
    }

    private static MethodModel BuildConversionOperator(
        SourceFile file, ClassModel cls, ConversionOperatorDeclarationSyntax node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new MethodModel
        {
            Name = $"operator {node.ImplicitOrExplicitKeyword.Text} {node.Type}",
            IsConstructor = false,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = ModelBuilderHelpers.IsExported(node.Modifiers),
            IsPrivate = ModelBuilderHelpers.IsPrivate(node.Modifiers),
            Parameters = ModelBuilderHelpers.BuildParameters(node.ParameterList),
            ReturnType = node.Type.ToString(),
            Class = cls,
            Node = node,
            Body = node.Body,
            File = file,
        };
    }

    private static MethodModel BuildDestructor(
        SourceFile file, ClassModel cls, DestructorDeclarationSyntax node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new MethodModel
        {
            Name = $"~{node.Identifier.Text}",
            IsConstructor = false,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Exported = false,
            IsPrivate = false,
            Parameters = ModelBuilderHelpers.BuildParameters(node.ParameterList),
            ReturnType = "",
            Class = cls,
            Node = node,
            Body = node.Body,
            File = file,
        };
    }
}
