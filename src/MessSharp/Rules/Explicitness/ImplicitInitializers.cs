using MessSharp.Model;

namespace MessSharp.Rules.Explicitness;

internal static class ImplicitInitializers
{
    /// <summary>The implicit constructor runs each instance field initializer of the class and its same-file bases.</summary>
    internal static DeferredResolution Resolve(IReadOnlyList<CallableOwner> owners, ClassModel cls)
    {
        var work = new List<DeferredWork>();
        var seen = new HashSet<ClassModel>();
        for (var current = cls; current is not null && seen.Add(current); current = ConstructorTypes.BaseClass(current))
        {
            if (current != cls && HasExplicitConstructor(owners, current)) return new DeferredResolution([], false);
            work.AddRange(owners.Where(o => o.Method.Class == current && FieldInitializerOwners.IsInstance(o))
                .Select(initializer => new DeferredWork(DeferredKind.Lazy, initializer,
                    ConstructorTypes.Invocation(current.Name, null), new Dictionary<string, StorageOrigin>())));
        }
        return new DeferredResolution(work, true);
    }

    private static bool HasExplicitConstructor(IReadOnlyList<CallableOwner> owners, ClassModel cls) =>
        owners.Any(o => o.Parent is null && o.Method.Class == cls && o.Method.IsConstructor && !o.Method.IsStaticConstructor());
}
