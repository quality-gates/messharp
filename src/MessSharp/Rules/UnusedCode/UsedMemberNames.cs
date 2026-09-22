using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// Collects the member names read by a type's code, for the unused private
/// member rules. Scans the current file plus the parts of the same partial
/// type declared in other files.
/// </summary>
internal static class UsedMemberNames
{
    /// <summary>
    /// Collects every identifier that appears as: a member-access selector
    /// (this.Name or x.Name), an object-initializer key, or a nameof()
    /// argument — all of which count as "read" for field-usage purposes.
    /// Also collects bare identifier reads (covers access without `this.`),
    /// except when the identifier is only a plain-assignment target or is
    /// lexically bound to a local variable or parameter that shadows the
    /// member name.
    /// </summary>
    public static HashSet<string> Collect(RuleContext ctx, ClassModel cls)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        CollectUsedNames(ctx.File.Root, used);
        foreach (var part in ctx.Partials.PartsInOtherFiles(cls))
            CollectUsedNames(part.Node, used);
        return used;
    }

    private static void CollectUsedNames(SyntaxNode root, HashSet<string> used)
    {
        var shadowed = ShadowMap.From(root);
        foreach (var node in root.DescendantNodes())
            CollectUsedNode(node, shadowed, used);
    }

    private static void CollectUsedNode(SyntaxNode node, ShadowMap shadowed, HashSet<string> used)
    {
        if (TryCollectMemberAccess(node, used)) return;
        if (TryCollectInitializerAssignment(node, used)) return;
        if (TryCollectNameof(node, used)) return;
        CollectBareIdentifier(node, shadowed, used);
    }

    private static bool TryCollectMemberAccess(SyntaxNode node, HashSet<string> used)
    {
        var mae = node as MemberAccessExpressionSyntax;
        if (mae is null) return false;

        if (!IsWriteOnlyAssignmentTarget(mae))
            used.Add(mae.Name.Identifier.Text);
        return true;
    }

    private static bool TryCollectInitializerAssignment(SyntaxNode node, HashSet<string> used)
    {
        var aes = node as AssignmentExpressionSyntax;
        if (aes is null) return false;

        var initializer = aes.Parent as InitializerExpressionSyntax;
        if (initializer is null) return false;

        if (aes.Left is IdentifierNameSyntax lhs)
            used.Add(lhs.Identifier.Text);
        return true;
    }

    private static void CollectBareIdentifier(SyntaxNode node, ShadowMap shadowed, HashSet<string> used)
    {
        var id = node as IdentifierNameSyntax;
        if (id is null) return;
        if (IsDeclarationContext(id)) return;
        if (IsWriteOnlyAssignmentTarget(id)) return;
        if (shadowed.IsShadowed(id)) return;
        used.Add(id.Identifier.Text);
    }

    private static bool IsWriteOnlyAssignmentTarget(SyntaxNode node)
    {
        if (node.Parent is AssignmentExpressionSyntax assignment)
            return IsSimpleAssignmentTarget(node, assignment);

        if (node.Parent is MemberAccessExpressionSyntax member
            && ReferenceEquals(member.Name, node)
            && member.Parent is AssignmentExpressionSyntax memberAssignment)
        {
            return IsSimpleAssignmentTarget(member, memberAssignment);
        }

        if (IsTupleDeconstructionTarget(node))
            return true;

        return false;
    }

    /// <summary>
    /// Recognises identifier/member targets inside a tuple deconstruction
    /// assignment's LHS (e.g. `(_a, _b) = (1, 2)` or `(_a, this._b) = ...`),
    /// which are pure writes just like a simple assignment target.
    /// </summary>
    private static bool IsTupleDeconstructionTarget(SyntaxNode node)
    {
        var arg = node.Parent as ArgumentSyntax;
        if (arg is null) return false;

        var tuple = arg.Parent as TupleExpressionSyntax;
        if (tuple is null) return false;
        return IsSimpleAssignmentTargetTuple(tuple);
    }

    private static bool IsSimpleAssignmentTargetTuple(TupleExpressionSyntax tuple)
    {
        if (tuple.Parent is AssignmentExpressionSyntax assignment)
        {
            return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && ReferenceEquals(assignment.Left, tuple);
        }

        // nested tuple target, e.g. (_a, (_b, _c)) = ...
        return tuple.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax outer }
            && IsSimpleAssignmentTargetTuple(outer);
    }

    private static bool IsSimpleAssignmentTarget(
        SyntaxNode target,
        AssignmentExpressionSyntax assignment)
    {
        var isInitializer = assignment.Parent is InitializerExpressionSyntax;
        return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && !isInitializer
            && ReferenceEquals(assignment.Left, target);
    }

    private static bool TryCollectNameof(SyntaxNode node, HashSet<string> used)
    {
        var inv = node as InvocationExpressionSyntax;
        if (inv is null) return false;

        var id2 = inv.Expression as IdentifierNameSyntax;
        if (id2 is null) return false;
        if (id2.Identifier.Text != "nameof") return false;
        if (inv.ArgumentList.Arguments.Count != 1) return false;

        var arg = inv.ArgumentList.Arguments[0].Expression;
        if (arg is IdentifierNameSyntax nameofArg)
            used.Add(nameofArg.Identifier.Text);
        else if (arg is MemberAccessExpressionSyntax nameofMae)
            used.Add(nameofMae.Name.Identifier.Text);
        return true;
    }

    private static bool IsDeclarationContext(IdentifierNameSyntax id)
    {
        // variable declarator, field declarator, parameter — these are
        // definitions not reads; exclude them from the "used" set.
        var parent = id.Parent;
        if (parent is VariableDeclaratorSyntax vd && vd.Identifier.Text == id.Identifier.Text)
            return true;
        return false;
    }
}
