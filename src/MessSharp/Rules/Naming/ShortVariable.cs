using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.Rules;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Naming;

/// <summary>
/// Reports fields, parameters, and local variables whose names are shorter
/// than the configured minimum length. Skips for-loop initializer variables
/// (matching phpmd's behavior). Port of phpmd's ShortVariable rule.
/// </summary>
public sealed class ShortVariableRule : BaseRule, IClassRule, IMethodRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        var exceptions = GetExceptions(ctx);
        int min = ctx.Props.Int("minimum", 3);
        foreach (var f in cls.Fields)
            CheckName(ctx, f.Name, f.Line, min, exceptions);
    }

    public void Apply(RuleContext ctx, MethodModel method)
    {
        var exceptions = GetExceptions(ctx);
        int min = ctx.Props.Int("minimum", 3);

        foreach (var p in method.Parameters)
            CheckName(ctx, p.Name, p.Line, min, exceptions);

        if (method.EffectiveBody != null)
        {
            foreach (var (name, line, isLoop) in CollectLocals(method.EffectiveBody))
            {
                if (isLoop) continue;
                CheckName(ctx, name, line, min, exceptions);
            }
        }
    }

    private static void CheckName(RuleContext ctx, string name, int line,
        int min, HashSet<string> exceptions)
    {
        if (name.Length >= min) return;
        if (exceptions.Contains(name)) return;
        ctx.Report(line, line, name, min);
    }

    private static HashSet<string> GetExceptions(RuleContext ctx) =>
        new(ctx.Props.Str("exceptions", "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.Ordinal);

    /// <summary>
    /// Walks a method body and yields (name, line, isLoop) for local declarations,
    /// local deconstruction declarations, foreach variables, and direct <c>is</c>
    /// declaration patterns. isLoop = true when the declarator is the initializer
    /// of a for-statement (phpmd skips those).
    /// </summary>
    internal static IEnumerable<(string Name, int Line, bool IsLoop)> CollectLocals(
        Microsoft.CodeAnalysis.SyntaxNode body)
    {
        foreach (var node in body.DescendantNodes())
        {
            foreach (var local in CollectNodeLocals(node))
                yield return local;
        }
    }

    private static IEnumerable<(string Name, int Line, bool IsLoop)> CollectNodeLocals(
        Microsoft.CodeAnalysis.SyntaxNode node) =>
        node switch
        {
            VariableDeclarationSyntax varDecl when LocalVariableCollector.IsLocalStatementDeclaration(varDecl) =>
                CollectFromVariableDeclaration(varDecl),
            AssignmentExpressionSyntax or DeclarationExpressionSyntax =>
                CollectFromExpression(node),
            DeclarationPatternSyntax pattern =>
                CollectFromPattern(pattern),
            ForEachStatementSyntax forEach =>
                CollectFromForEach(forEach),
            ForEachVariableStatementSyntax forEachVariable =>
                CollectFromForEachVariable(forEachVariable),
            _ => Enumerable.Empty<(string Name, int Line, bool IsLoop)>(),
        };

    private static IEnumerable<(string Name, int Line, bool IsLoop)> CollectFromVariableDeclaration(
        VariableDeclarationSyntax varDecl)
    {
        bool isForInit = varDecl.Parent is ForStatementSyntax;
        foreach (var (name, line) in LocalVariableCollector.CollectVariables(varDecl))
            yield return (name, line, isForInit);
    }

    private static IEnumerable<(string Name, int Line, bool IsLoop)> CollectFromExpression(
        Microsoft.CodeAnalysis.SyntaxNode node)
    {
        var variables = new List<(string Name, int Line)>();
        LocalVariableCollector.CollectDeclarationNames(node, variables);
        foreach (var (name, line) in variables)
            yield return (name, line, false);
    }

    private static IEnumerable<(string Name, int Line, bool IsLoop)> CollectFromPattern(
        DeclarationPatternSyntax pattern)
    {
        foreach (var (name, line) in LocalVariableCollector.DeclarationPatternVariables(pattern))
            yield return (name, line, false);
    }

    private static IEnumerable<(string Name, int Line, bool IsLoop)> CollectFromForEach(
        ForEachStatementSyntax forEach)
    {
        if (forEach.Identifier.Text != "_")
        {
            var span = forEach.SyntaxTree.GetLineSpan(forEach.Identifier.Span);
            int line = span.StartLinePosition.Line + 1;
            yield return (forEach.Identifier.Text, line, false);
        }
    }

    private static IEnumerable<(string Name, int Line, bool IsLoop)> CollectFromForEachVariable(
        ForEachVariableStatementSyntax forEachVariable)
    {
        var variables = new List<(string Name, int Line)>();
        LocalVariableCollector.CollectDeclarationNames(
            forEachVariable.Variable, forEachVariable.SyntaxTree, variables);
        foreach (var (name, line) in variables)
            yield return (name, line, false);
    }
}
