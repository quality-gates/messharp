using MessSharp.Model;
using MessSharp.Rule;
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

        if (method.Body != null)
        {
            foreach (var (name, line, isLoop) in CollectLocals(method.Body))
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
    /// Walks a method body and yields (name, line, isLoop) for every local
    /// variable declaration. isLoop = true when the declarator is the
    /// initializer of a for-statement (phpmd skips those).
    /// </summary>
    internal static IEnumerable<(string Name, int Line, bool IsLoop)> CollectLocals(
        Microsoft.CodeAnalysis.CSharp.Syntax.BlockSyntax body)
    {
        foreach (var node in body.DescendantNodes())
        {
            if (node is not LocalDeclarationStatementSyntax local)
                continue;

            bool isForInit = local.Parent is ForStatementSyntax;
            foreach (var v in local.Declaration.Variables)
            {
                var span = v.SyntaxTree.GetLineSpan(v.Span);
                int line = span.StartLinePosition.Line + 1;
                yield return (v.Identifier.Text, line, isForInit);
            }
        }
    }
}
