using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class MethodEffects
{
    internal static readonly IReadOnlySet<string> NoNames = new HashSet<string>();
    private static readonly MethodEffects None = new();

    public IReadOnlyList<StateAccess> Statics { get; init; } = [];

    public IReadOnlySet<string> MutableStatics { get; init; } = NoNames;

    public IReadOnlyList<StateAccess> Instance { get; init; } = [];

    public IReadOnlyList<StateAccess> Parameters { get; init; } = [];

    public IReadOnlyList<Finding> AmbientInputs { get; init; } = [];

    public IReadOnlyList<Finding> AmbientOutputs { get; init; } = [];

    public static MethodEffects Empty { get; } = None;
}

internal static class MethodEffectAnalyzer
{
    private static readonly IReadOnlySet<string> NoNames = MethodEffects.NoNames;

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
        string? Root(ExpressionSyntax expression) => names.Parameters.Resolve(expression)
            ?? (names.Statics.Resolve(expression) is { } stat ? StorageRoots.Static(stat) : null)
            ?? (names.Instance.Resolve(expression) is { } member ? StorageRoots.Member(member) : null);
        var ownership = new ArgumentOwnership(body, Root, owner, owners);
        var mutableParameters = method.Parameters.Where(p => StorageTypes.IsMutable(p.Type))
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var parameters = new StateAccessCollector(ownership.Resolve, mutableParameters, Mutation, readsMutations: false);
        var mutations = StateAccessCollector.MutationsOf(ownership.MutationRoots, Mutation);
        var ambient = new AmbientUses(owner);
        BodyWalk.Run(body, [statics, instance, parameters, mutations, ambient]);
        return new MethodEffects
        {
            Statics = statics.Accesses(),
            MutableStatics = names.MutableStatics,
            Instance = instance.Accesses(),
            Parameters = parameters.Accesses().Where(a => a.Kind == AccessKind.Read)
                .Concat(mutations.Accesses()).ToList(),
            AmbientInputs = ambient.Inputs(),
            AmbientOutputs = ambient.Outputs(),
        };
    }

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

        private static bool OwnsInstance(MethodModel method) => !method.IsConstructor && !method.IsStatic();
    }
}
