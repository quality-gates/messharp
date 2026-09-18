using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// Reports method/constructor parameters that are never referenced in the body.
/// Port of messgo's UnusedFormalParameter; C# adaptations:
///   - `out var` parameters (the parameter itself) still count if referenced
///   - params named `_` are ignored (explicit discard pattern)
///   - expression-bodied members are checked via BodyAnalysis.EffectiveBody
/// </summary>
public sealed class UnusedFormalParameterRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.Parameters.Count == 0) return;
        var body = BodyAnalysis.EffectiveBody(method);
        if (body == null) return;   // abstract / extern / interface declaration

        var reads = CollectReads(method, body);
        HashSet<string>? writes = null;

        foreach (var p in method.Parameters)
        {
            if (string.IsNullOrEmpty(p.Name) || p.Name == "_") continue;
            if (reads.Contains(p.Name)) continue;

            if (p.IsOut)
            {
                writes ??= BodyAnalysis.IdentWrites(body);
                if (writes.Contains(p.Name)) continue;
            }

            ctx.Report(p.Line, p.Line, p.Name);
        }
    }

    /// <summary>
    /// Reads from the effective body plus, for constructors, the `: base(...)`/`: this(...)`
    /// initializer, which is a sibling of the body in the syntax tree rather than a descendant.
    /// </summary>
    private static HashSet<string> CollectReads(MethodModel method, SyntaxNode body)
    {
        var reads = BodyAnalysis.IdentReads(body);
        if (method.Node is ConstructorDeclarationSyntax { Initializer: not null } ctor)
        {
            reads.UnionWith(BodyAnalysis.IdentReads(ctor.Initializer));
        }

        return reads;
    }
}
