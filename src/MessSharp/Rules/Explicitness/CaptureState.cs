using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class CaptureState
{
    public static bool Contains(CallableOwner owner, string name) => owner.Parent is not null
        && owner.Method.Parameters.Skip(CallableOwner.Parameters(owner.Method.Node).Count()).Any(p => p.Name == name);

    public static bool MayChange(CallableOwner owner, string name, IReadOnlyList<CallableOwner> owners)
    {
        if (owner.Parent is not { } parent) return false;
        if (parent.Method.Parameters.Any(p => p.Name == name && StorageTypes.IsMutable(p.Type))) return true;
        return owners.Where(o => Within(o, parent)).Any(o => Changes(o, name, owners));
    }

    private static bool Within(CallableOwner owner, CallableOwner parent)
    {
        for (var scope = owner; scope is not null; scope = scope.Parent)
            if (scope == parent) return true;
        return false;
    }

    private static bool Changes(CallableOwner owner, string name, IReadOnlyList<CallableOwner> owners)
    {
        if (owner.Body is null) return false;
        var names = StateNames.Parameters(new HashSet<string> { name });
        var accesses = new StateAccessCollector(names.Resolve,
            knownMutation: call => KnownMutations.IsCollectionMutation(owner, call))
        { AccessorCalls = EventSubscriptions.Assignments(owner, owners, owner.Body) };
        BodyWalk.Run(owner.Body, [accesses]);
        return accesses.Accesses().Any(a => a.Kind != AccessKind.Read);
    }
}
