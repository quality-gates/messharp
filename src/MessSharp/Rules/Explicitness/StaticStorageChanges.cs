using MessSharp.Model;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class StaticStorageChanges
{
    public static IEnumerable<string> Find(ClassModel part, IReadOnlySet<string> names)
    {
        var owners = CallableOwner.Gather(part.File);
        var statics = StateNames.StaticMembers(names, part.Name);
        string? Root(ExpressionSyntax expression) => statics.Resolve(expression) is { } name ? StorageRoots.Static(name) : null;
        foreach (var owner in owners.Where(o => o.Method.Class == part && !o.Method.IsStaticConstructor()))
        {
            if (owner.Body is not { } body) continue;
            var ownership = new ArgumentOwnership(body, Root, owner, owners);
            var changes = StateAccessCollector.MutationsOf(ownership.MutationRoots,
                call => KnownMutations.IsCollectionMutation(owner, call), EventSubscriptions.Assignments(owner, owners, body));
            BodyWalk.Run(body, [changes]);
            foreach (var access in changes.Accesses())
                if (access.Name.StartsWith("static:", StringComparison.Ordinal) && names.Contains(access.Name[7..]))
                    yield return access.Name[7..];
        }
    }
}
