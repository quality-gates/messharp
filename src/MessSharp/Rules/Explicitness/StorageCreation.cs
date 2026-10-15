using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageCreation(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    public StorageOrigin? Create(ExpressionSyntax expression) => expression switch
    {
        ArrayCreationExpressionSyntax array => Array(array, array.Initializer),
        ImplicitArrayCreationExpressionSyntax array => Array(array, array.Initializer),
        BaseObjectCreationExpressionSyntax creation => Object(creation),
        CollectionExpressionSyntax collection => Collection(collection),
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } call
            when member.Name.Identifier.Text is "Clone" or "ToList" or "ToArray" => Copy(call, member.Expression),
        _ => null,
    };

    private StorageOrigin Array(ExpressionSyntax expression, InitializerExpressionSyntax? initializer)
    {
        var receiver = Allocate(expression, new StorageCell { Length = initializer?.Expressions.Count });
        new StorageInitializers(context, origin, state).Apply(receiver, initializer);
        return receiver;
    }

    private StorageOrigin Object(BaseObjectCreationExpressionSyntax creation)
    {
        var receiver = new StorageAllocations(context, state).Object(creation, origin);
        if (ConstructorCall.Resolve(context.Owner, context.Owners, creation) is { } constructor)
            new StorageCalls(context, origin, state).Construct(constructor, creation, receiver, context.Weak);
        new StorageInitializers(context, origin, state).Apply(receiver, creation.Initializer);
        return receiver;
    }

    private StorageOrigin Collection(CollectionExpressionSyntax collection)
    {
        var receiver = Allocate(collection, new StorageCell { Length = 0 });
        new StorageInitializers(context, origin, state).Apply(receiver, collection);
        return receiver;
    }

    private StorageOrigin Copy(ExpressionSyntax call, ExpressionSyntax source) => Allocate(call, state.Heap.CopyElements(origin(source)));

    private StorageOrigin Allocate(ExpressionSyntax expression, StorageCell cell) =>
        new StorageAllocations(context, state).Allocate(expression, cell);
}
