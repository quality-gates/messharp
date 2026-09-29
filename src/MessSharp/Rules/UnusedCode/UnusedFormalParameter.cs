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
///   - class/struct primary constructor parameters are checked across the
///     whole type declaration (see PrimaryConstructorScope)
/// </summary>
public sealed class UnusedFormalParameterRule : BaseRule, IMethodRule
{
    public void Apply(RuleContext ctx, MethodModel method)
    {
        if (method.Parameters.Count == 0) return;
        var scope = Scope(method);
        if (scope == null) return;   // abstract / extern / interface declaration, record, partial type

        var (body, reads) = scope.Value;
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
    /// The syntax the parameters are visible in, with the names read there,
    /// or null when the method has nothing to check.
    /// </summary>
    private static (SyntaxNode Body, HashSet<string> Reads)? Scope(MethodModel method)
    {
        if (method.Node is ParameterListSyntax)
        {
            var type = PrimaryConstructorScope.CheckableType(method);
            return type == null ? null : (type, PrimaryConstructorScope.Reads(type));
        }

        var body = BodyAnalysis.EffectiveBody(method);
        return body == null ? null : (body, CollectReads(method, body));
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
