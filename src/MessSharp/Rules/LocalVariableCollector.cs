using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules;

/// <summary>
/// Shared collection helpers for C# variable designations.
/// </summary>
internal static class LocalVariableCollector
{
    /// <summary>
    /// Returns true if the variable declaration is part of a local, for, using, or fixed statement.
    /// </summary>
    internal static bool IsLocalStatementDeclaration(VariableDeclarationSyntax declaration) =>
        declaration.Parent is LocalDeclarationStatementSyntax
            or ForStatementSyntax
            or UsingStatementSyntax
            or FixedStatementSyntax;

    /// <summary>
    /// Collects declared variable names from a variable declaration node
    /// (e.g. from local, for, using, or fixed statements).
    /// Excludes '_' discards.
    /// </summary>
    internal static IEnumerable<(string Name, int Line)> CollectVariables(
        VariableDeclarationSyntax declaration)
    {
        foreach (var v in declaration.Variables)
        {
            if (v.Identifier.Text != "_")
            {
                var line = v.SyntaxTree.GetLineSpan(v.Span).StartLinePosition.Line + 1;
                yield return (v.Identifier.Text, line);
            }
        }
    }

    /// <summary>
    /// Collects variables declared by a direct <c>is</c> declaration pattern.
    /// Nested recursive patterns are intentionally outside this rule's scope.
    /// </summary>
    internal static List<(string Name, int Line)> DeclarationPatternVariables(
        DeclarationPatternSyntax pattern)
    {
        var result = new List<(string Name, int Line)>();
        if (pattern.Parent is not IsPatternExpressionSyntax)
            return result;

        CollectDesignation(pattern.Designation, pattern.SyntaxTree, result);
        return result;
    }

    /// <summary>
    /// Collects variables declared by a deconstruction expression, such as a
    /// local deconstruction or a deconstructed foreach variable.
    /// </summary>
    internal static void CollectDeclarationNames(ExpressionSyntax expr, SyntaxTree tree,
        List<(string Name, int Line)> result)
    {
        if (expr is DeclarationExpressionSyntax decl)
            CollectDesignation(decl.Designation, tree, result);
    }

    private static void CollectDesignation(VariableDesignationSyntax designation,
        SyntaxTree tree, List<(string Name, int Line)> result)
    {
        switch (designation)
        {
            case SingleVariableDesignationSyntax single:
                if (single.Identifier.Text != "_")
                {
                    var line = tree.GetLineSpan(single.Span).StartLinePosition.Line + 1;
                    result.Add((single.Identifier.Text, line));
                }
                break;
            case ParenthesizedVariableDesignationSyntax parenthesized:
                foreach (var child in parenthesized.Variables)
                    CollectDesignation(child, tree, result);
                break;
        }
    }
}
