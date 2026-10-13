using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ExpressionTargetTypes(CallableOwner owner, Func<ExpressionSyntax, string?> typeOf)
{
    public string? Find(ExpressionSyntax expression) => expression.Parent switch
    {
        ParenthesizedExpressionSyntax or ConditionalExpressionSyntax => Find((ExpressionSyntax)expression.Parent),
        SwitchExpressionArmSyntax { Parent: SwitchExpressionSyntax choice } => Find(choice),
        _ => Direct(expression),
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
