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
        var exceptions = ctx.Props.Set("exceptions");
        int min = ctx.Props.Int("minimum", 3);
        foreach (var f in cls.Fields)
            CheckName(ctx, f.Name, f.Line, min, exceptions);
    }

    public void Apply(RuleContext ctx, MethodModel method)
    {
        var exceptions = ctx.Props.Set("exceptions");
        int min = ctx.Props.Int("minimum", 3);

        foreach (var p in method.Parameters)
            CheckName(ctx, p.Name, p.Line, min, exceptions);

        if (method.EffectiveBody != null)
        {
            foreach (var local in NamedLocals(method.EffectiveBody))
            {
                if (local.Kind == LocalKind.ForInitializer) continue;
                CheckName(ctx, local.Name, local.Line, min, exceptions);
            }
        }
    }

    private static void CheckName(RuleContext ctx, string name, int line,
        int min, IReadOnlySet<string> exceptions)
    {
        if (name.Length >= min) return;
        if (exceptions.Contains(name)) return;
        ctx.Report(line, line, name, min);
    }

    /// <summary>
    /// Declared locals that naming rules check: everything the shared walk finds
    /// except catch variables and switch-case patterns.
    /// </summary>
    internal static IEnumerable<LocalDeclaration> NamedLocals(Microsoft.CodeAnalysis.SyntaxNode body) =>
        LocalDeclarations.Collect(body)
            .Where(l => l.Kind is not (LocalKind.Catch or LocalKind.CasePattern));
}
