using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// What a method body does to state and to the world outside the program,
/// found in one walk and shared by the explicitness rules of one analysis.
/// Each rule applies its own policy to these effects.
/// </summary>
internal sealed class MethodEffects
{
    internal static readonly IReadOnlySet<string> NoNames = new HashSet<string>();
    private static readonly MethodEffects None = new();

    /// <summary>Accesses to the class's static state. Empty for a static constructor.</summary>
    public IReadOnlyList<StateAccess> Statics { get; init; } = [];

    /// <summary>Static state that can get a new value after type initialization.</summary>
    public IReadOnlySet<string> MutableStatics { get; init; } = NoNames;

    /// <summary>Accesses to the instance's own state. Empty for constructors and static members.</summary>
    public IReadOnlyList<StateAccess> Instance { get; init; } = [];

    /// <summary>Accesses to the method's parameters.</summary>
    public IReadOnlyList<StateAccess> Parameters { get; init; } = [];

    /// <summary>Uses of the clock, environment, console input, file reads and random sources.</summary>
    public IReadOnlyList<Finding> AmbientInputs { get; init; } = [];

    /// <summary>Uses of the console, debug trace, file writes and environment changes.</summary>
    public IReadOnlyList<Finding> AmbientOutputs { get; init; } = [];

    public static MethodEffects Empty { get; } = None;
}

internal static class MethodEffectAnalyzer
{
    private static readonly IReadOnlySet<string> NoNames = MethodEffects.NoNames;

    /// <summary>The method's effects, analyzed once per analysis.</summary>
    public static MethodEffects Analyze(RuleContext ctx, CallableOwner owner, IReadOnlyList<CallableOwner> owners)
    {
        var body = owner.Body;
        if (body is null) return MethodEffects.Empty;
        return AnalyzeBody(ctx, owner, owners, body);
    }

    private static MethodEffects AnalyzeBody(
        RuleContext ctx, CallableOwner owner, IReadOnlyList<CallableOwner> owners, SyntaxNode body)
    {
        var method = owner.Method;
        var names = StateInScope.For(ctx, method);
        bool Mutation(SyntaxNodeCall call) => KnownMutations.IsCollectionMutation(owner, call);
        var statics = new StateAccessCollector(names.Statics.Resolve, knownMutation: Mutation);
        var instance = new StateAccessCollector(names.Instance.Resolve, knownMutation: Mutation);
        var ownership = new ArgumentOwnership(body, names.Parameters.Resolve, owner, owners);
        var mutableParameters = method.Parameters.Where(p => StorageTypes.IsMutable(p.Type))
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var parameters = new StateAccessCollector(ownership.Resolve, mutableParameters, Mutation);
        var ambient = new AmbientUses(owner);
        BodyWalk.Run(body, [statics, instance, parameters, ambient]);
        return new MethodEffects
        {
            Statics = statics.Accesses(),
            MutableStatics = names.MutableStatics,
            Instance = instance.Accesses(),
            Parameters = parameters.Accesses(),
            AmbientInputs = ambient.Inputs(),
            AmbientOutputs = ambient.Outputs(),
        };
    }

    /// <summary>
    /// The names each kind of state resolves against in one method. A kind
    /// that the method is exempt from has no names.
    /// </summary>
    private sealed record StateInScope(
        StateNames Statics, IReadOnlySet<string> MutableStatics, StateNames Instance, StateNames Parameters)
    {
        public static StateInScope For(RuleContext ctx, MethodModel method)
        {
            var parameters = StateNames.Parameters(method.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal));
            if (method.Class is not { } cls)
                return new StateInScope(NoStatics(""), NoNames, StateNames.InstanceMembers(NoNames), parameters);
            var state = ClassState.For(ctx, cls);
            var statics = method.IsStaticConstructor() ? NoStatics(cls.Name) : StateNames.StaticMembers(state.Statics, cls.Name);
            var instance = StateNames.InstanceMembers(OwnsInstance(method) ? state.Instance : NoNames);
            return new StateInScope(statics, state.MutableStatics, instance, parameters);
        }

        private static StateNames NoStatics(string className) => StateNames.StaticMembers(NoNames, className);

        /// <summary>Constructors set up the instance state and static members have none.</summary>
        private static bool OwnsInstance(MethodModel method) => !method.IsConstructor && !method.IsStatic();
    }
}
