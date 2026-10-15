using System.Runtime.CompilerServices;
using MessSharp.Model;

namespace MessSharp.Rules.Explicitness;

/// <summary>One owner list per file, shared by every type lookup that lacks the call graph's owners.</summary>
internal static class CallableOwnerCache
{
    private static readonly ConditionalWeakTable<SourceFile, IReadOnlyList<CallableOwner>> Owners = new();

    public static IReadOnlyList<CallableOwner> For(SourceFile file) => Owners.GetValue(file, CallableOwner.Gather);
}
