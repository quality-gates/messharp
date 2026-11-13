using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

/// <summary>What one execution of a callee body leaves behind for its caller.</summary>
internal sealed record StorageCallResult(StorageOrigin Returned, StorageHeap Heap,
    IReadOnlyDictionary<string, StorageDeferredValue> Deferred);

/// <summary>
/// Callee executions keyed by everything that drives them: the callee context, the recursion guard and the entry
/// environment. Each query site and fixpoint pass otherwise re-executes the same callees from the same state.
/// </summary>
internal static class StorageCallMemo
{
    private static readonly ConditionalWeakTable<SyntaxNode, Dictionary<StorageCallKey, StorageCallResult>> Results = new();

    public static StorageCallResult For(StorageContext callee, CallableOwner target, Certainty weak, StorageEnvironment initial)
    {
        var results = Results.GetOrCreateValue(callee.Body);
        initial.ForgetCallsOutside(callee.Scope);
        var key = Key(callee, weak, initial);
        if (results.TryGetValue(key, out var cached)) return cached;
        var result = Run(callee, target, weak, initial);
        results[key] = result;
        return result;
    }

    private static StorageCallResult Run(StorageContext callee, CallableOwner target, Certainty weak, StorageEnvironment initial)
    {
        using var active = callee.Visiting.Enter(target);
        var flow = new StorageFlow(callee);
        var final = flow.Before(callee.Body, initial, true);
        var returned = StorageResults.Collect(callee.Body, flow, initial, final, weak);
        return new StorageCallResult(returned, final.Heap, final.Deferred);
    }

    private static StorageCallKey Key(StorageContext callee, Certainty weak, StorageEnvironment initial)
    {
        var visiting = string.Join(",", callee.Visiting.Items.Select(InstanceKeys.For).Order(StringComparer.Ordinal));
        return new StorageCallKey(callee.Scope, callee.Weak, weak, InstanceKeys.For(callee.Owner!),
            InstanceKeys.For(callee.Owners), visiting, StorageFingerprint.For(initial));
    }
}

/// <summary>Everything that drives a callee execution, so two executions with equal keys give equal results.</summary>
internal readonly record struct StorageCallKey(string Scope, Certainty CalleeWeak, Certainty CallWeak,
    string Owner, string Owners, string Visiting, string Entry);
