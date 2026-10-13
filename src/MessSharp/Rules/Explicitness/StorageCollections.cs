using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageCollections(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    public bool Mutate(InvocationExpressionSyntax call, bool weak)
    {
        if (call.Expression is not MemberAccessExpressionSyntax member || context.Owner is not { } owner
            || !KnownMutations.IsCollectionMutation(owner, new SyntaxNodeCall(member.Expression, member.Name.Identifier.Text))) return false;
        var name = member.Name.Identifier.Text;
        var receiver = origin(member.Expression);
        var mutation = Mutation(call, name) ?? new StorageMutation.Reorder();
        state.Heap.Change(receiver, mutation, weak || IsRange(name));
        return true;
    }

    private StorageMutation? Mutation(InvocationExpressionSyntax call, string name)
    {
        if (name == "Clear") return new StorageMutation.Clear();
        if (name == "Insert") return Insert(call);
        if (StorageCollectionEdits.Find(call, name, origin) is { } edit) return edit;
        if (call.ArgumentList.Arguments.LastOrDefault() is not { } argument) return null;
        var value = origin(argument.Expression);
        if (IsRange(name)) return new StorageMutation.Append(state.Heap.Read(value, StorageCell.AnyElement), context.Identity(call));
        return IsAppend(name) ? new StorageMutation.Append(value, context.Identity(call)) : null;
    }

    private StorageMutation? Insert(InvocationExpressionSyntax call)
    {
        var arguments = call.ArgumentList.Arguments;
        if (arguments.Count != 2) return null;
        var index = arguments[0].Expression is LiteralExpressionSyntax literal ? literal.Token.Value as int? : null;
        return new StorageMutation.Insert(origin(arguments[1].Expression), index, context.Identity(call));
    }

    private static bool IsRange(string name) => name is "AddRange" or "InsertRange" or "UnionWith";

    private static bool IsAppend(string name) => name is "Add" or "Enqueue" or "Push" or "TryAdd";
}
