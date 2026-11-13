using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class ValueBranches
{
    /// <summary>The values a conditional, switch or coalescing expression can produce.</summary>
    public static IReadOnlyList<ExpressionSyntax>? Find(ExpressionSyntax value) => value switch
    {
        ConditionalExpressionSyntax choice => LiteralControlFlow.Boolean(choice.Condition) switch
        {
            true => [choice.WhenTrue],
            false => [choice.WhenFalse],
            _ => [choice.WhenTrue, choice.WhenFalse],
        },
        SwitchExpressionSyntax choice => choice.Arms.Select(arm => arm.Expression).ToList(),
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce => [coalesce.Left, coalesce.Right],
        _ => null,
    };
}
