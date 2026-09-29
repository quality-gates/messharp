using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// Recognises member references that only write a field: simple assignment
/// targets, tuple deconstruction targets and `out` arguments, whether bare
/// (`_f`) or member-qualified (`this._f`). Used by <see cref="UsedMemberNames"/>
/// so a written-but-never-read field still counts as unused.
/// </summary>
internal static class WriteOnlyTarget
{
    internal static bool Matches(SyntaxNode node)
    {
        // the name in `this._f` is a write exactly when `this._f` is
        var target = EnclosingMemberAccess(node) ?? node;

        if (target.Parent is AssignmentExpressionSyntax assignment)
            return IsSimpleAssignmentTarget(target, assignment);

        return IsTupleDeconstructionTarget(target) || IsOutArgument(target);
    }

    private static MemberAccessExpressionSyntax? EnclosingMemberAccess(SyntaxNode node) =>
        node.Parent is MemberAccessExpressionSyntax member && ReferenceEquals(member.Name, node)
            ? member
            : null;

    /// <summary>
    /// Recognises `Helper(out _f)` / `Helper(out this._f)`: the callee must
    /// assign the field and never sees its previous value, so it is a pure write.
    /// </summary>
    private static bool IsOutArgument(SyntaxNode node) =>
        node.Parent is ArgumentSyntax arg
            && arg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword)
            && ReferenceEquals(arg.Expression, node);

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
}
