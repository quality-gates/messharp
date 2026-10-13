using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageWrites(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    public bool Assign(AssignmentExpressionSyntax assignment, bool weak)
    {
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && !assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression)) return false;
        var target = Target(assignment.Left);
        if (target is null) return false;
        state.Heap.Write(target.Value.Receiver, target.Value.Slot, origin(assignment.Right),
            weak || assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression));
        return true;
    }

    private (StorageOrigin Receiver, string Slot)? Target(ExpressionSyntax expression)
    {
        if (StorageMembers.OwnField(context, expression) is { } own
            && state.Bindings.TryGetValue(StorageContext.ThisBinding, out var receiver)) return (receiver, own);
        if (expression is MemberAccessExpressionSyntax member && StorageMembers.IsField(context, member))
            return (origin(member.Expression), "." + member.Name.Identifier.Text);
        if (expression is ElementAccessExpressionSyntax element && context.Owner is { } owner
            && !PropertyDispatch.IsIndexer(owner, element.Expression))
            return (origin(element.Expression), StorageMembers.Index(element.ArgumentList));
        return null;
    }

}
