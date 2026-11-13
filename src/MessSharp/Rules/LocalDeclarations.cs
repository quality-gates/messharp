using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules;

/// <summary>How a local name is introduced; rules filter on this instead of re-walking the AST.</summary>
internal enum LocalKind
{
    /// <summary>Local, using, or fixed statement declaration.</summary>
    Statement,
    /// <summary>Declaration in a for-statement initializer.</summary>
    ForInitializer,
    Foreach,
    /// <summary>Deconstruction, tuple, or <c>out var</c> declaration expression.</summary>
    DeclarationExpression,
    /// <summary>Direct <c>is</c> declaration pattern.</summary>
    IsPattern,
    /// <summary>Declaration pattern in a switch case label or switch-expression arm.</summary>
    CasePattern,
    Catch,
    LambdaParameter,
}

internal readonly record struct LocalDeclaration(string Name, int Line, LocalKind Kind);

/// <summary>
/// The single walk that discovers declared local names in a method body.
/// Naming, controversial and unused-code rules share it and select the
/// <see cref="LocalKind"/>s they care about. Excludes '_' discards.
/// </summary>
internal static class LocalDeclarations
{
    internal static List<LocalDeclaration> Collect(SyntaxNode body)
    {
        var result = new List<LocalDeclaration>();
        foreach (var node in body.DescendantNodesAndSelf())
            CollectNode(node, result);
        return result;
    }

    private static void CollectNode(SyntaxNode node, List<LocalDeclaration> result)
    {
        if (!TryCollectStatementForm(node, result))
            CollectExpressionForm(node, result);
    }

    private static bool TryCollectStatementForm(SyntaxNode node, List<LocalDeclaration> result)
    {
        switch (node)
        {
            case VariableDeclarationSyntax vd when LocalVariableCollector.IsLocalStatementDeclaration(vd):
                var kind = vd.Parent is ForStatementSyntax ? LocalKind.ForInitializer : LocalKind.Statement;
                AddAll(result, LocalVariableCollector.CollectVariables(vd), kind);
                return true;
            case ForEachStatementSyntax fe:
                AddIdentifier(result, fe.Identifier, LocalKind.Foreach);
                return true;
            case ForEachVariableStatementSyntax feVar:
                AddDeclaration(result, feVar.Variable, feVar.SyntaxTree, LocalKind.Foreach);
                return true;
            case CatchDeclarationSyntax catchDecl:
                AddAll(result, LocalVariableCollector.CatchVariables(catchDecl), LocalKind.Catch);
                return true;
            default:
                return false;
        }
    }

    private static void CollectExpressionForm(SyntaxNode node, List<LocalDeclaration> result)
    {
        switch (node)
        {
            case AssignmentExpressionSyntax { Left: DeclarationExpressionSyntax left } assignment:
                AddDeclaration(result, left, assignment.SyntaxTree, LocalKind.DeclarationExpression);
                break;
            case DeclarationExpressionSyntax decl when decl.Parent is ArgumentSyntax:
                AddDeclaration(result, decl, decl.SyntaxTree, LocalKind.DeclarationExpression);
                break;
            case DeclarationPatternSyntax pattern:
                AddPattern(result, pattern);
                break;
            case ParameterSyntax parameter when IsLambdaParameter(parameter):
                AddIdentifier(result, parameter.Identifier, LocalKind.LambdaParameter);
                break;
        }
    }

    private static void AddPattern(List<LocalDeclaration> result, DeclarationPatternSyntax pattern)
    {
        var direct = LocalVariableCollector.DeclarationPatternVariables(pattern);
        if (direct.Count > 0)
        {
            AddAll(result, direct, LocalKind.IsPattern);
            return;
        }
        AddAll(result, LocalVariableCollector.TopLevelPatternVariables(pattern), LocalKind.CasePattern);
    }

    private static void AddDeclaration(List<LocalDeclaration> result, ExpressionSyntax expr,
        SyntaxTree tree, LocalKind kind)
    {
        var names = new List<(string Name, int Line)>();
        LocalVariableCollector.CollectDeclarationNames(expr, tree, names);
        AddAll(result, names, kind);
    }

    private static void AddIdentifier(List<LocalDeclaration> result, SyntaxToken identifier, LocalKind kind)
    {
        if (identifier.Text == "_") return;
        var line = identifier.SyntaxTree!.GetLineSpan(identifier.Span).StartLinePosition.Line + 1;
        result.Add(new LocalDeclaration(identifier.Text, line, kind));
    }

    private static void AddAll(List<LocalDeclaration> result,
        IEnumerable<(string Name, int Line)> names, LocalKind kind)
    {
        foreach (var (name, line) in names)
            result.Add(new LocalDeclaration(name, line, kind));
    }

    private static bool IsLambdaParameter(ParameterSyntax parameter) =>
        parameter.Parent is SimpleLambdaExpressionSyntax
        || parameter.Parent is ParameterListSyntax
        { Parent: ParenthesizedLambdaExpressionSyntax or AnonymousMethodExpressionSyntax };
}
