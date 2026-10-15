using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageDeferred(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    private readonly HashSet<string> _forcing = new(StringComparer.Ordinal);

    public StorageOrigin? Value(ExpressionSyntax expression, bool weak)
    {
        if (expression is not MemberAccessExpressionSyntax member || member.Name.Identifier.Text != "Value"
            || context.Owner is not { } owner || !KnownExecutionOperations.IsLazy(owner, member.Expression)) return null;
        var receiver = origin(member.Expression);
        Force(receiver, member, weak);
        return state.Heap.Read(receiver, ".Value");
    }

    public bool Execute(SyntaxNode node, bool weak)
    {
        var sequence = StorageForcingSites.Sequence(node);
        if (sequence is not null) { Force(origin(sequence), node, weak || StorageForcingSites.MayStopEarly(node)); return true; }
        return node is ExpressionSyntax expression && Value(expression, weak) is not null;
    }

    public void BeforeCall(InvocationExpressionSyntax call, bool weak)
    {
        if (context.Owner is not { } owner || call.Expression is not MemberAccessExpressionSyntax member) return;
        var force = KnownExecutionOperations.IsSequenceForce(owner, call);
        var callback = KnownExecutionOperations.IsEagerCallback(owner, call);
        if (!force && !callback) return;
        var receiver = origin(member.Expression);
        if (force) Force(receiver, call, weak || StorageForcingSites.MayStopEarly(call));
        Callbacks(call, receiver);
    }

    private void Callbacks(InvocationExpressionSyntax call, StorageOrigin receiver)
    {
        if (context.Owner is not { } owner || !KnownExecutionOperations.IsEagerCallback(owner, call)) return;
        var work = new StorageCallbackWork(context, origin, state).From(call, state.Heap.Read(receiver, StorageCell.AnyElement));
        foreach (var task in work) new StorageCalls(context, origin, state).ExecuteDeferred(task, call, true);
    }

    private void Force(StorageOrigin receiver, SyntaxNode site, bool weak)
    {
        foreach (var identity in receiver.Objects.ToList())
        {
            if (!state.Deferred.TryGetValue(identity, out var value) || !_forcing.Add(identity)) continue;
            try { ForceOne(identity, value, site, weak || receiver.Objects.Count != 1); }
            finally { _forcing.Remove(identity); }
        }
    }

    private void ForceOne(string identity, StorageDeferredValue value, SyntaxNode site, bool weak)
    {
        if (value.Kind == DeferredKind.Lazy && value.Status == StorageDeferredStatus.Forced) return;
        weak |= value.Status == StorageDeferredStatus.MaybeForced;
        if (value.Source is { } source) Force(source, site, weak);
        var returned = Produced(value, site, weak);
        var receiver = StorageOrigin.Private with { Objects = new HashSet<string> { identity } };
        if (value.Kind == DeferredKind.Lazy)
        {
            state.Heap.Write(receiver, ".Value", returned, weak);
            state.Deferred[identity] = value with { Status = weak ? StorageDeferredStatus.MaybeForced : StorageDeferredStatus.Forced };
            return;
        }
        var elements = value.UsesSource && value.Source is { } sequence ? state.Heap.Read(sequence, StorageCell.AnyElement) : returned;
        state.Heap.ReplaceElements(receiver, elements, weak);
    }

    private StorageOrigin Produced(StorageDeferredValue value, SyntaxNode site, bool weak)
    {
        var returned = StorageOrigin.Private;
        foreach (var work in value.Work)
        {
            var task = value.Source is { } sequence ? work.WithElements(state.Heap.Read(sequence, StorageCell.AnyElement)) : work;
            returned = returned.Join(new StorageCalls(context, origin, state).ExecuteDeferred(task, site, weak || value.Source is not null));
        }
        return returned;
    }

}
