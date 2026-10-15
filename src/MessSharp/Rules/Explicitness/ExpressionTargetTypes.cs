using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ExpressionTargetTypes(CallableOwner owner, Func<ExpressionSyntax, string?> typeOf)
{
    public string? Find(ExpressionSyntax expression) => expression.Parent switch
    {
        ParenthesizedExpressionSyntax or ConditionalExpressionSyntax => Find((ExpressionSyntax)expression.Parent),
        SwitchExpressionArmSyntax { Parent: SwitchExpressionSyntax choice } => Find(choice),
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce => Find(coalesce),
        InitializerExpressionSyntax initializer => ExpressionTypes.Element(Collection(initializer)),
        ExpressionElementSyntax { Parent: CollectionExpressionSyntax list } => ExpressionTypes.Element(Find(list)),
        _ => Direct(expression),
    };

    /// <summary>The collection type an array or collection initializer fills.</summary>
    private string? Collection(InitializerExpressionSyntax initializer) => initializer.Parent switch
    {
        ArrayCreationExpressionSyntax array => array.Type.ToString(),
        ImplicitArrayCreationExpressionSyntax array => Find(array),
        BaseObjectCreationExpressionSyntax creation when initializer.IsKind(SyntaxKind.CollectionInitializerExpression) =>
            typeOf(creation),
        _ => null,
    };

    private string? Direct(ExpressionSyntax expression) => expression.Parent switch
    {
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } } =>
            declaration.Type.ToString(),
        AssignmentExpressionSyntax assignment when assignment.Right == expression => typeOf(assignment.Left),
        ReturnStatementSyntax or ArrowExpressionClauseSyntax or LambdaExpressionSyntax => ReturnType(),
        CastExpressionSyntax cast => cast.Type.ToString(),
        _ => null,
    };

    private string? ReturnType() => owner.Method.ReturnType.Length == 0 ? null : owner.Method.ReturnType;
}
