using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// A <c>ref</c> local that is only assigned or passed on by <c>ref</c>: pointing it at storage reads nothing, so the
/// storage it names is not an input. Any other use of the alias may read the storage, so the declaration still counts.
/// </summary>
internal static class WriteOnlyAlias
{
    /// <summary>The expression when it may read storage: not a pure write and not a <c>ref</c> pass-through.</summary>
    public static ExpressionSyntax? ReadSite(SyntaxNode node, ISet<SyntaxNode> pureWrites) =>
        node is ExpressionSyntax expr && !pureWrites.Contains(expr) && !IsRefArgument(expr) ? expr : null;

    /// <summary>Passing storage by <c>ref</c> reads nothing: the callee's own effects say whether it reads or writes.</summary>
    private static bool IsRefArgument(ExpressionSyntax expr) =>
        expr is IdentifierNameSyntax { Parent: ArgumentSyntax { RefKindKeyword.RawKind: (int)SyntaxKind.RefKeyword } }
        || Initializes(expr);

    public static bool Initializes(ExpressionSyntax expression) =>
        expression.Parent is RefExpressionSyntax { Parent: EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax alias } }
        && alias.Parent is VariableDeclarationSyntax { Type: RefTypeSyntax }
        && expression.Ancestors().OfType<BlockSyntax>().LastOrDefault() is { } scope
        && scope.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(use => use.Identifier.Text == alias.Identifier.Text).All(IsWriteOrPass);

    private static bool IsWriteOrPass(IdentifierNameSyntax use) => use.Parent switch
    {
        ArgumentSyntax argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword),
        AssignmentExpressionSyntax assignment => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) && assignment.Left == use,
        _ => false,
    };
}
