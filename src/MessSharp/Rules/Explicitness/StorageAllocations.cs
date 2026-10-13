using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageAllocations(StorageContext context, StorageEnvironment state)
{
    public StorageOrigin Allocate(ExpressionSyntax expression, StorageCell cell) =>
        state.Heap.Allocate(context.Identity(expression), cell, context.Weak || new BodyBindings(context.Body).MayBeSkipped(expression));

    public StorageOrigin Object(BaseObjectCreationExpressionSyntax creation, Func<ExpressionSyntax, StorageOrigin> origin)
    {
        var cell = new StorageCell { Length = 0 };
        if (IsCollection(creation) && creation.ArgumentList?.Arguments is { Count: 1 } arguments)
            cell = state.Heap.CopyElements(origin(arguments[0].Expression));
        if (creation.Initializer is { } initializer) cell.Length = initializer.Expressions.Count;
        return Allocate(creation, cell);
    }

    private bool IsCollection(ExpressionSyntax creation) => context.Owner is { } owner
        && new ExpressionTypes(owner).TypeOf(creation) is { } type
        && StorageTypes.IsMutable(type) && !KnownTypeChecks.IsCustomType(owner, type);
}
