using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

[Flags]
internal enum ExecutionTiming { ColdQuery = 1, EagerCallback = 2, ForceSequence = 4 }

internal static class KnownExecutionOperations
{
    private static readonly IReadOnlyDictionary<string, ExecutionTiming> Timings =
        new Dictionary<string, ExecutionTiming>(StringComparer.Ordinal)
        {
            ["Select"] = ExecutionTiming.ColdQuery,
            ["Where"] = ExecutionTiming.ColdQuery,
            ["Any"] = ExecutionTiming.EagerCallback | ExecutionTiming.ForceSequence,
            ["All"] = ExecutionTiming.EagerCallback | ExecutionTiming.ForceSequence,
            ["Count"] = ExecutionTiming.EagerCallback | ExecutionTiming.ForceSequence,
            ["Sum"] = ExecutionTiming.EagerCallback | ExecutionTiming.ForceSequence,
            ["Min"] = ExecutionTiming.EagerCallback | ExecutionTiming.ForceSequence,
            ["Max"] = ExecutionTiming.EagerCallback | ExecutionTiming.ForceSequence,
            ["Sort"] = ExecutionTiming.EagerCallback,
            ["ForEach"] = ExecutionTiming.EagerCallback,
            ["ToList"] = ExecutionTiming.ForceSequence,
            ["ToArray"] = ExecutionTiming.ForceSequence,
        };
    public static bool IsIterator(CallableOwner owner) => owner.Body is not null
        && BodyWalk.Nodes(owner.Body).OfType<YieldStatementSyntax>().Any();

    public static bool IsSequence(CallableOwner owner, ExpressionSyntax receiver, IReadOnlyList<CallableOwner>? owners = null)
    {
        var type = new ExpressionTypes(owner, owners).TypeOf(receiver);
        if (type is null || KnownTypeChecks.IsCustomType(owner, type)) return false;
        var name = type.Split('<')[0].Split('.').Last();
        return StorageTypes.IsMutable(type) || name is "IEnumerable" or "IReadOnlyList" or "IList" or "ICollection";
    }

    public static bool IsLazy(CallableOwner owner, ExpressionSyntax value)
    {
        var type = new ExpressionTypes(owner).TypeOf(value);
        return type is not null && LazyTypes.IsLazy(owner, type);
    }

    public static bool IsLazyValue(CallableOwner owner, ExpressionSyntax value) =>
        new ExpressionTypes(owner).TypeOf(value) is { } type && LazyTypes.HasStandardValue(owner, type);

    public static bool IsColdQuery(CallableOwner owner, InvocationExpressionSyntax call, IReadOnlyList<CallableOwner>? owners = null) =>
        call.Expression is MemberAccessExpressionSyntax member && HasTiming(member, ExecutionTiming.ColdQuery)
        && IsSequence(owner, member.Expression, owners);

    public static bool IsEagerCallback(CallableOwner owner, InvocationExpressionSyntax call, IReadOnlyList<CallableOwner>? owners = null) =>
        call.Expression is MemberAccessExpressionSyntax member
        && HasTiming(member, ExecutionTiming.EagerCallback)
        && call.ArgumentList.Arguments.Count > 0 && IsSequence(owner, member.Expression, owners);

    public static bool IsSequenceForce(CallableOwner owner, InvocationExpressionSyntax call) =>
        call.Expression is MemberAccessExpressionSyntax member
        && HasTiming(member, ExecutionTiming.ForceSequence)
        && IsSequence(owner, member.Expression);

    private static bool HasTiming(MemberAccessExpressionSyntax member, ExecutionTiming timing) =>
        Timings.TryGetValue(member.Name.Identifier.Text, out var known) && (known & timing) != 0;

    public static bool RequiresExecution(CallableOwner owner, ExpressionSyntax value, DeferredKind kind)
    {
        if (kind != DeferredKind.Sequence) return true;
        var type = new ExpressionTypes(owner).TypeOf(value);
        return type is null || !StorageTypes.IsMutable(type);
    }
}
