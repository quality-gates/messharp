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
        var target = new StorageReferences(context, origin, state).Target(assignment.Left);
        if (target is null) return false;
        target.Write(state.Heap, origin(assignment.Right),
            weak || assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression));
        return true;
    }

}
