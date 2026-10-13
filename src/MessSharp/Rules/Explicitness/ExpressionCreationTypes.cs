using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class ExpressionCreationTypes
{
    public static string? Find(ExpressionSyntax expression, Func<IEnumerable<ExpressionSyntax>, string?> agreed) => expression switch
    {
        ObjectCreationExpressionSyntax creation => creation.Type.ToString(),
        ArrayCreationExpressionSyntax array => array.Type.ElementType + string.Concat(array.Type.RankSpecifiers
            .Select(rank => "[" + new string(',', rank.Rank - 1) + "]")),
        ImplicitArrayCreationExpressionSyntax array => agreed(array.Initializer.Expressions) is { } element ? element + "[]" : null,
        ImplicitObjectCreationExpressionSyntax
        {
            Parent: EqualsValueClauseSyntax
            {
                Parent: VariableDeclaratorSyntax
                { Parent: VariableDeclarationSyntax declaration }
            }
        } => declaration.Type.ToString(),
        _ => null,
    };
}
