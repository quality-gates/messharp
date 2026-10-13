using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class ConstructorDefaults
{
    public static bool IsEmpty(CallableOwner owner, string type, BaseObjectCreationExpressionSyntax creation)
    {
        if (creation.ArgumentList?.Arguments.Count > 0) return false;
        var name = type.Split('<')[0].Split('.').Last();
        var declarations = owner.Method.File.Classes.Where(cls => cls.Name == name)
            .Select(cls => cls.Node).OfType<ClassDeclarationSyntax>().Take(2).ToList();
        return declarations.Count == 1 && IsEmpty(declarations[0]);
    }

    private static bool IsEmpty(ClassDeclarationSyntax declaration) => declaration.ParameterList is null
        && declaration.BaseList is null && !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
        && !declaration.Members.Any(Initializes);

    private static bool Initializes(MemberDeclarationSyntax member) => member switch
    {
        ConstructorDeclarationSyntax => true,
        FieldDeclarationSyntax field => field.Declaration.Variables.Any(variable => variable.Initializer is not null),
        EventFieldDeclarationSyntax field => field.Declaration.Variables.Any(variable => variable.Initializer is not null),
        PropertyDeclarationSyntax property => property.Initializer is not null,
        _ => false,
    };
}
