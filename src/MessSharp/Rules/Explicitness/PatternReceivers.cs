using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The value a property pattern's subpattern reads a member of.</summary>
internal static class PatternReceivers
{
    /// <summary>The tested expression when <paramref name="node"/> is a <c>Value:</c> subpattern of a pattern applied directly to it.</summary>
    public static ExpressionSyntax? ValueReceiver(SyntaxNode node) =>
        node is SubpatternSyntax { NameColon.Name.Identifier.Text: "Value", Parent: PropertyPatternClauseSyntax { Parent: RecursivePatternSyntax pattern } }
            ? Tested(pattern) : null;

    private static ExpressionSyntax? Tested(SyntaxNode pattern)
    {
        SyntaxNode node = pattern;
        while (node.Parent is BinaryPatternSyntax or ParenthesizedPatternSyntax or UnaryPatternSyntax) node = node.Parent;
        return node.Parent switch
        {
            IsPatternExpressionSyntax test when test.Pattern == node => test.Expression,
            SwitchExpressionArmSyntax { Parent: SwitchExpressionSyntax switchExpression } arm when arm.Pattern == node =>
                switchExpression.GoverningExpression,
            CasePatternSwitchLabelSyntax { Parent: SwitchSectionSyntax { Parent: SwitchStatementSyntax statement } } label
                when label.Pattern == node => statement.Expression,
            _ => null,
        };
    }
}
