using MessSharp.Model;
using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The accesses to the instance's own state in an instance method. Constructors
/// are left out: setting up that state is what they are for.
/// </summary>
internal static class InstanceStateAccesses
{
    public static IEnumerable<StateAccess> Collect(RuleContext ctx, MethodModel method)
    {
        if (method.Class is not { } cls || method.IsConstructor || method.IsStatic()) return [];
        if (method.EffectiveBody is not { } body) return [];
        var names = StateNames.InstanceMembers(ClassState.Gather(ctx, cls).Instance);
        return StateAccessCollector.Collect(body, names.Resolve);
    }
}
