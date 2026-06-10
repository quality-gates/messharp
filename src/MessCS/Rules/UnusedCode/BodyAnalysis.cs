using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Rules.UnusedCode;

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
    ///   - `nameof(x)` — counts as a read of x
    ///   - `this.field` — the field name counts as a read
    /// </summary>
    internal static HashSet<string> IdentReads(SyntaxNode body)
    {
        var writes = CollectWriteIdents(body);
        var reads = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in body.DescendantNodesAndSelf())
        {
            switch (node)
            {
                // nameof(x) or nameof(T.x) count as a read
                case InvocationExpressionSyntax inv
                    when inv.Expression is IdentifierNameSyntax nameofKw
                      && nameofKw.Identifier.Text == "nameof"
                      && inv.ArgumentList.Arguments.Count == 1:
                    var argExpr = inv.ArgumentList.Arguments[0].Expression;
                    if (argExpr is IdentifierNameSyntax nameofId)
                        reads.Add(nameofId.Identifier.Text);
                    else if (argExpr is MemberAccessExpressionSyntax nameofMae)
                        reads.Add(nameofMae.Name.Identifier.Text);
                    break;

                // member access: this.field or obj.member
                case MemberAccessExpressionSyntax mae:
                    reads.Add(mae.Name.Identifier.Text);
                    break;

                // bare identifier that is not a pure write
                case IdentifierNameSyntax id when !writes.Contains(id):
                    if (id.Identifier.Text != "_")
                        reads.Add(id.Identifier.Text);
                    break;
            }
        }
        return reads;
    }

    /// <summary>
    /// Collects the specific IdentifierNameSyntax nodes that appear only as
    /// assignment or declaration targets (pure writes). These are excluded
    /// from reads. We track node identity (not name) to handle shadowing.
    /// </summary>
    private static HashSet<SyntaxNode> CollectWriteIdents(SyntaxNode body)
    {
        var writes = new HashSet<SyntaxNode>(ReferenceEqualityComparer.Instance);

        foreach (var node in body.DescendantNodesAndSelf())
        {
            switch (node)
            {
                // simple assignment or compound assignment LHS
                case AssignmentExpressionSyntax aes
                    when aes.Left is IdentifierNameSyntax lhsId:
                    writes.Add(lhsId);
                    break;

                // local variable declaration: var x = ...
                // The IdentifierToken is on the VariableDeclaratorSyntax; the
                // IdentifierNameSyntax in the type position is something else.
                // We mark the declarator itself; callers check via IsDeclaratorId.
                case VariableDeclaratorSyntax vd:
                    // handled below — we need the IdentifierNameSyntax not the token
                    break;

                // out-variable declarations: Method(out var x) / Method(out Type x)
                case DeclarationExpressionSyntax decl
                    when decl.Designation is SingleVariableDesignationSyntax svd:
                    // These are write-only declarations; we do NOT add them to reads
                    // The name will appear as a IdentifierNameSyntax in the designation
                    // but is not a real identifier node — nothing to mark.
                    break;
            }
        }

        return writes;
    }

    /// <summary>
    /// Collects declared local variable names from a body node.
    /// Returns (name, line) pairs. Excludes `_` discards.
    /// Covers: LocalDeclarationStatement, foreach variables, pattern variables,
    /// out-variable declarations, and range-loop variables.
    /// </summary>
    internal static List<(string Name, int Line)> LocalVariables(SyntaxNode body)
    {
        var result = new List<(string, int)>();

        foreach (var node in body.DescendantNodesAndSelf())
        {
            switch (node)
            {
                // var x = expr; or int x = expr;
                case LocalDeclarationStatementSyntax ld:
                    foreach (var v in ld.Declaration.Variables)
                    {
                        var name = v.Identifier.Text;
                        if (name == "_") continue;
                        var line = v.SyntaxTree.GetLineSpan(v.Span).StartLinePosition.Line + 1;
                        result.Add((name, line));
                    }
                    break;

                // foreach (var item in ...) or foreach (var (k,v) in ...)
                case ForEachStatementSyntax fe:
                {
                    var name = fe.Identifier.Text;
                    if (name != "_")
                    {
                        var line = fe.SyntaxTree.GetLineSpan(fe.Identifier.Span).StartLinePosition.Line + 1;
                        result.Add((name, line));
                    }
                    break;
                }

                // foreach (var (k, v) in dict) — DeconstructionForeachStatement
                case ForEachVariableStatementSyntax feVar:
                    CollectDesignationNames(feVar.Variable, feVar.SyntaxTree, result);
                    break;

                // out var x in argument lists
                case DeclarationExpressionSyntax decl
                    when decl.Designation is SingleVariableDesignationSyntax outVar:
                {
                    var name = outVar.Identifier.Text;
                    if (name != "_")
                    {
                        var line = decl.SyntaxTree.GetLineSpan(decl.Span).StartLinePosition.Line + 1;
                        result.Add((name, line));
                    }
                    break;
                }
            }
        }

        return result;
    }

    private static void CollectDesignationNames(ExpressionSyntax expr, SyntaxTree tree,
        List<(string, int)> result)
    {
        // For deconstruction foreach: var (a, b) in ...
        if (expr is DeclarationExpressionSyntax decl)
        {
            CollectDesignation(decl.Designation, tree, result);
        }
    }

    private static void CollectDesignation(VariableDesignationSyntax des, SyntaxTree tree,
        List<(string, int)> result)
    {
        switch (des)
        {
            case SingleVariableDesignationSyntax sv:
                if (sv.Identifier.Text != "_")
                {
                    var line = tree.GetLineSpan(sv.Span).StartLinePosition.Line + 1;
                    result.Add((sv.Identifier.Text, line));
                }
                break;
            case ParenthesizedVariableDesignationSyntax pv:
                foreach (var child in pv.Variables)
                    CollectDesignation(child, tree, result);
                break;
        }
    }

    /// <summary>
    /// Returns the effective body syntax node for a method. Handles both
    /// block bodies `{ ... }` and expression bodies `=> expr`.
    /// </summary>
    internal static SyntaxNode? EffectiveBody(MessCS.Model.MethodModel method)
    {
        if (method.Body != null) return method.Body;

        // expression-bodied member: int Foo() => expr;
        var node = method.Node;
        var exprBody = node switch
        {
            MethodDeclarationSyntax m => m.ExpressionBody?.Expression,
            ConstructorDeclarationSyntax c => c.ExpressionBody?.Expression,
            _ => null,
        };
        return exprBody;
    }
}
