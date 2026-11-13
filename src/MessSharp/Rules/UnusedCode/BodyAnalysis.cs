using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MessSharp.Rules;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// Shared helpers for analysing method / accessor bodies.
/// Mirrors messgo's identReads + LocalVariables utilities, adapted to C#.
/// </summary>
internal static class BodyAnalysis
{
    /// <summary>
    /// Returns the set of identifier names that are *read* somewhere in a
    /// syntax node (body, expression, etc.) — i.e. not purely on the
    /// left-hand side of an assignment or in a declaration.
    /// Matches messgo's identReads semantics for C# specifics:
    ///   - `_ = expr`  — the discard `_` is excluded (already excluded by name)
    ///   - `out var x` — `x` is a write-only declaration, not a read
    ///   - `nameof(x)` — counts as a read of bare identifier x
    ///   - member accesses (`obj.member`, `this.member`) — member names do not count as reads of locals/parameters
    /// </summary>
    internal static HashSet<string> IdentReads(SyntaxNode body) =>
        IdentReads(body, _ => false);

    /// <summary>
    /// As <see cref="IdentReads(SyntaxNode)"/>, but skips identifiers for which
    /// <paramref name="isExcluded"/> returns true (e.g. names bound to a
    /// shadowing declaration).
    /// </summary>
    internal static HashSet<string> IdentReads(SyntaxNode body, Func<IdentifierNameSyntax, bool> isExcluded)
    {
        var writes = WriteIdentCollector.Collect(body);
        var reads = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in body.DescendantNodesAndSelf())
            CollectRead(node, writes, isExcluded, reads);

        return reads;
    }

    private static void CollectRead(
        SyntaxNode node, HashSet<SyntaxNode> writes,
        Func<IdentifierNameSyntax, bool> isExcluded, HashSet<string> reads)
    {
        if (TryCollectNameofRead(node, isExcluded, reads)) return;

        if (node is IdentifierNameSyntax id
            && !writes.Contains(id)
            && !isExcluded(id)
            && id.Identifier.Text != "_"
            && !IsMemberOrCallLabel(id))
        {
            reads.Add(id.Identifier.Text);
        }
    }

    private static bool IsMemberOrCallLabel(IdentifierNameSyntax id)
    {
        return id.Parent switch
        {
            MemberAccessExpressionSyntax mae => ReferenceEquals(mae.Name, id),
            MemberBindingExpressionSyntax mbe => ReferenceEquals(mbe.Name, id),
            NameColonSyntax nameColon => ReferenceEquals(nameColon.Name, id),
            NameEqualsSyntax nameEquals => ReferenceEquals(nameEquals.Name, id),
            InvocationExpressionSyntax inv
                when ReferenceEquals(inv.Expression, id) && id.Identifier.Text == "nameof" => true,
            _ => false,
        };
    }

    private static bool TryCollectNameofRead(
        SyntaxNode node, Func<IdentifierNameSyntax, bool> isExcluded, HashSet<string> reads)
    {
        if (node is not InvocationExpressionSyntax inv || !IsSingleArgumentNameof(inv)) return false;

        var argExpr = inv.ArgumentList.Arguments[0].Expression;
        if (argExpr is IdentifierNameSyntax nameofId && nameofId.Identifier.Text != "_" && !isExcluded(nameofId))
            reads.Add(nameofId.Identifier.Text);
        return true;
    }

    private static bool IsSingleArgumentNameof(InvocationExpressionSyntax inv) =>
        inv.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" }
        && inv.ArgumentList.Arguments.Count == 1;

    /// <summary>
    /// Collects the set of identifier names that are written to as assignment targets
    /// (including tuple deconstruction targets) or passed as `out` arguments in a syntax node.
    /// </summary>
    internal static HashSet<string> IdentWrites(SyntaxNode body)
    {
        var writes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in body.DescendantNodesAndSelf())
            CollectWrite(node, writes);
        return writes;
    }

    private static void CollectWrite(SyntaxNode node, HashSet<string> writes)
    {
        switch (node)
        {
            case AssignmentExpressionSyntax { Left: IdentifierNameSyntax lhsId }:
                writes.Add(lhsId.Identifier.Text);
                break;

            // (x, y) = (1, 2); writes every identifier target in the tuple.
            case AssignmentExpressionSyntax { Left: TupleExpressionSyntax tuple }:
                foreach (var id in WriteIdentCollector.TupleTargetIdents(tuple))
                    writes.Add(id.Identifier.Text);
                break;

            case ArgumentSyntax { Expression: IdentifierNameSyntax outId } arg
                when arg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword):
                writes.Add(outId.Identifier.Text);
                break;
        }
    }

    /// <summary>
    /// Declared local (name, line) pairs from a body node: every declaration the
    /// shared walk finds except lambda parameters. Excludes `_` discards.
    /// </summary>
    internal static List<(string Name, int Line)> LocalVariables(SyntaxNode body) =>
        LocalDeclarations.Collect(body)
            .Where(l => l.Kind != LocalKind.LambdaParameter)
            .Select(l => (l.Name, l.Line))
            .ToList();

    /// <summary>
    /// Collects the (name, line) pairs of locals declared as <c>using</c>
    /// resources. Disposal at scope exit reads them, so they are never unused.
    /// </summary>
    internal static HashSet<(string Name, int Line)> UsingResourceVariables(SyntaxNode body) =>
        body.DescendantNodesAndSelf()
            .OfType<VariableDeclarationSyntax>()
            .Where(LocalVariableCollector.IsUsingResourceDeclaration)
            .SelectMany(LocalVariableCollector.CollectVariables)
            .ToHashSet();

    /// <summary>
    /// Returns the effective body syntax node for a method. Handles both
    /// block bodies `{ ... }` and expression bodies `=> expr`.
    /// </summary>
    internal static SyntaxNode? EffectiveBody(MessSharp.Model.MethodModel method) =>
        method.EffectiveBody;
}
