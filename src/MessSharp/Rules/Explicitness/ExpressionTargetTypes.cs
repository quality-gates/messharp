using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ExpressionTargetTypes(CallableOwner owner, Func<ExpressionSyntax, string?> typeOf)
{
    public string? Find(ExpressionSyntax expression) => expression.Parent switch
    {
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } } =>
            declaration.Type.ToString(),
        AssignmentExpressionSyntax assignment when assignment.Right == expression => typeOf(assignment.Left),
        ReturnStatementSyntax or ArrowExpressionClauseSyntax => ReturnType(),
        CastExpressionSyntax cast => cast.Type.ToString(),
        ParenthesizedExpressionSyntax or ConditionalExpressionSyntax => Find((ExpressionSyntax)expression.Parent),
        SwitchExpressionArmSyntax { Parent: SwitchExpressionSyntax choice } => Find(choice),
        _ => null,
    };

    private string? ReturnType() => owner.Method.ReturnType.Length == 0 ? null : owner.Method.ReturnType;
}
