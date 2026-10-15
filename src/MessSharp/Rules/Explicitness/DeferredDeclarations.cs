using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Finds the declaration a reference reads: a local, an out or pattern variable, a parameter of the
/// reading owner, or a field or auto-property of its class.
/// </summary>
internal sealed class DeferredDeclarations(CallableOwner owner)
{
    public SyntaxNode? Find(ExpressionSyntax reference) => reference switch
    {
        IdentifierNameSyntax name => Identifier(name),
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } member => Member(member.Name.Identifier.Text),
        MemberAccessExpressionSyntax member when member.Expression.ToString() == owner.Method.Class?.Name =>
            Member(member.Name.Identifier.Text),
        _ => null,
    };

    private SyntaxNode? Identifier(IdentifierNameSyntax name)
    {
        var variable = Local(name);
        if (variable is VariableDeclaratorSyntax declarator && owner.Body is { } body
            && RefAliasCache.For(body).Aliased(declarator) is { } aliased) return Find(aliased);
        if (variable is not null && !IsShared(variable)) return variable;
        return (SyntaxNode?)Parameter(name) ?? variable ?? Member(name.Identifier.Text);
    }

    /// <summary>Whether a declaration outlives one call, so any owner can write it.</summary>
    public static bool IsShared(SyntaxNode declaration) => declaration
        is VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax } or PropertyDeclarationSyntax;

    public static string Name(SyntaxNode declaration) => declaration switch
    {
        VariableDeclaratorSyntax variable => variable.Identifier.Text,
        SingleVariableDesignationSyntax designation => designation.Identifier.Text,
        ParameterSyntax parameter => parameter.Identifier.Text,
        PropertyDeclarationSyntax property => property.Identifier.Text,
        _ => "",
    };

    public bool IsOwnParameter(ParameterSyntax parameter) => DeclaredParameters.Declared(owner.Method.Node).Contains(parameter);

    private static SyntaxNode? Local(IdentifierNameSyntax name) =>
        (SyntaxNode?)ExpressionTypes.Binding(name) ?? DesignationTypes.Declaration(name);

    private static ParameterSyntax? Parameter(IdentifierNameSyntax name) => name.Ancestors()
        .TakeWhile(node => node is not BaseTypeDeclarationSyntax)
        .SelectMany(DeclaredParameters.Declared)
        .FirstOrDefault(parameter => parameter.Identifier.Text == name.Identifier.Text);

    /// <summary>Whether <paramref name="node"/> declares or writes <paramref name="declaration"/>.</summary>
    public bool IsWrittenBy(SyntaxNode node, SyntaxNode declaration) => node switch
    {
        VariableDeclaratorSyntax or SingleVariableDesignationSyntax => node == declaration,
        AssignmentExpressionSyntax { Left: TupleExpressionSyntax tuple } =>
            DeconstructionBindings.Targets(tuple).Any(target => Find(target) == declaration),
        AssignmentExpressionSyntax assignment => Find(assignment.Left) == declaration,
        ArgumentSyntax { RefKindKeyword.RawKind: (int)SyntaxKind.RefKeyword or (int)SyntaxKind.OutKeyword } argument =>
            Find(argument.Expression) == declaration,
        _ => false,
    };

    private SyntaxNode? Member(string name)
    {
        var field = owner.Method.Class?.Fields.FirstOrDefault(candidate => candidate.Name == name);
        return field?.Node switch
        {
            FieldDeclarationSyntax declaration =>
                declaration.Declaration.Variables.FirstOrDefault(variable => variable.Identifier.Text == name),
            PropertyDeclarationSyntax property => property,
            _ => null,
        };
    }
}
