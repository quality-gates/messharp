using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageInitializers(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    public void Apply(StorageOrigin receiver, InitializerExpressionSyntax? initializer)
    {
        if (initializer is null) return;
        var count = initializer.Expressions.Count;
        for (var index = 0; index < count; index++)
        {
            var value = initializer.Expressions[index];
            if (initializer.IsKind(SyntaxKind.ObjectInitializerExpression)) Member(receiver, value);
            else state.Heap.Write(receiver, "[" + index + "]", origin(value), false);
        }
    }

    private void Member(StorageOrigin receiver, ExpressionSyntax value)
    {
        if (value is not AssignmentExpressionSyntax assignment) return;
        var slot = assignment.Left switch
        {
            IdentifierNameSyntax name => "." + name.Identifier.Text,
            ImplicitElementAccessSyntax element => StorageMembers.Index(element.ArgumentList),
            _ => null,
        };
        if (slot is not null) state.Heap.Write(receiver, slot, origin(assignment.Right), false);
    }

    public void Apply(StorageOrigin receiver, CollectionExpressionSyntax collection)
    {
        foreach (var element in collection.Elements)
        {
            if (element is ExpressionElementSyntax item)
                state.Heap.Change(receiver, new StorageMutation.Append(origin(item.Expression), context.Identity(item)), false);
            else if (element is SpreadElementSyntax spread)
                state.Heap.Change(receiver, new StorageMutation.Append(
                    state.Heap.Read(origin(spread.Expression), StorageCell.AnyElement), context.Identity(spread)), true);
        }
    }
}
