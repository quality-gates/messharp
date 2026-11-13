using Microsoft.CodeAnalysis;
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
        var target = node.Parent is MemberAccessExpressionSyntax member && ReferenceEquals(member.Name, node) ? member : node;
        return MutationClassifier.IsPureWriteTarget(target);
    }
}
