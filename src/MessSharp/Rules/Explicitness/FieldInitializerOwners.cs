using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Field and auto-property initializers. Static ones run when the type initializes and belong to
/// <c>&lt;module&gt;</c>; instance ones run inside construction and are named after the constructor.
/// </summary>
internal static class FieldInitializerOwners
{
    public static IEnumerable<CallableOwner> Gather(SourceFile file) => file.Classes.SelectMany(cls =>
        cls.Node.ChildNodes().OfType<MemberDeclarationSyntax>().SelectMany(member => Initializers(member)
            .Select(initializer => Owner(file, cls, member, initializer))));

    public static bool IsInstance(CallableOwner owner) => owner.Method.Name != "<module>" && owner.Method.Node switch
    {
        VariableDeclaratorSyntax variable => variable.Initializer?.Value == owner.Body,
        PropertyDeclarationSyntax property => property.Initializer?.Value == owner.Body,
        _ => false,
    };

    private static IEnumerable<(SyntaxNode Node, ExpressionSyntax Value)> Initializers(MemberDeclarationSyntax member) =>
        member switch
        {
            FieldDeclarationSyntax field when !field.Modifiers.Any(SyntaxKind.ConstKeyword) => field.Declaration.Variables
                .Where(variable => variable.Initializer is not null)
                .Select(variable => ((SyntaxNode)variable, variable.Initializer!.Value)),
            PropertyDeclarationSyntax { Initializer: { } initializer } property => [(property, initializer.Value)],
            _ => [],
        };

    private static CallableOwner Owner(SourceFile file, ClassModel cls, MemberDeclarationSyntax member,
        (SyntaxNode Node, ExpressionSyntax Value) initializer) => new(new MethodModel
        {
            Name = member.Modifiers.Any(SyntaxKind.StaticKeyword) ? "<module>" : cls.Name,
            Node = initializer.Node,
            Class = cls,
            File = file,
        }, initializer.Value);
}
