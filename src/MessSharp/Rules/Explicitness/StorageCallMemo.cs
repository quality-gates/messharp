using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
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
    private static readonly ConditionalWeakTable<SyntaxNode, Dictionary<string, StorageCallResult>> Results = new();

    public static StorageCallResult For(StorageContext callee, CallableOwner target, bool weak, StorageEnvironment initial)
    {
        var results = Results.GetOrCreateValue(callee.Body);
        var key = Key(callee, weak, initial);
        if (results.TryGetValue(key, out var cached)) return cached;
        var result = Run(callee, target, weak, initial);
        results[key] = result;
        return result;
    }

    private static StorageCallResult Run(StorageContext callee, CallableOwner target, bool weak, StorageEnvironment initial)
    {
        callee.Visiting.Add(target);
        try
        {
            var flow = new StorageFlow(callee);
            var final = flow.Before(callee.Body, initial, true);
            var returned = StorageResults.Collect(callee.Body, flow, initial, final, weak);
            return new StorageCallResult(returned, final.Heap, final.Deferred);
        }
        finally { callee.Visiting.Remove(target); }
    }

    private static string Key(StorageContext callee, bool weak, StorageEnvironment initial)
    {
        var visiting = string.Join(",", callee.Visiting.Select(InstanceKeys.For).Order(StringComparer.Ordinal));
        var text = string.Join("|", callee.Scope, callee.Weak, weak, InstanceKeys.For(callee.Owner!),
            InstanceKeys.For(callee.Owners), visiting, StorageFingerprint.For(initial));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
