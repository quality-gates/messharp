using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Whether a member throws away the value of its expression body
/// (<c>=&gt; expr</c>). Such a body works like a single expression statement.
/// </summary>
internal static class ExpressionBodies
{
    public static bool DiscardsValue(ArrowExpressionClauseSyntax arrow) => arrow.Parent switch
    {
        MethodDeclarationSyntax m => ReturnsNothing(m.ReturnType, m.Modifiers),
        LocalFunctionStatementSyntax f => ReturnsNothing(f.ReturnType, f.Modifiers),
        ConstructorDeclarationSyntax => true,
        AccessorDeclarationSyntax a => !a.Keyword.IsKind(SyntaxKind.GetKeyword),
        _ => false,
    };

    /// <summary><c>void</c>, or <c>async</c> with a <c>Task</c> or <c>ValueTask</c> that has no result.</summary>
    private static bool ReturnsNothing(TypeSyntax returnType, SyntaxTokenList modifiers) =>
        (returnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        || (modifiers.Any(SyntaxKind.AsyncKeyword) && IsTaskWithoutResult(returnType));

    private static bool IsTaskWithoutResult(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.Text is "Task" or "ValueTask",
        QualifiedNameSyntax q => IsTaskWithoutResult(q.Right),
        _ => false,
    };
}
