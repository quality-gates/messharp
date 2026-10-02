using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// What a method body does to state and to the world outside the program,
/// found in one walk and shared by the explicitness rules of one analysis.
/// Each rule applies its own policy to these effects.
/// </summary>
internal sealed class MethodEffects
{
    private static readonly IReadOnlySet<string> NoNames = new HashSet<string>();
    private static readonly MethodEffects None = new();

    /// <summary>Accesses to the class's static state. Empty for a static constructor.</summary>
    public IReadOnlyList<StateAccess> Statics { get; private init; } = [];

    /// <summary>Static state that can get a new value after type initialization.</summary>
    public IReadOnlySet<string> MutableStatics { get; private init; } = NoNames;

    /// <summary>Accesses to the instance's own state. Empty for constructors and static members.</summary>
    public IReadOnlyList<StateAccess> Instance { get; private init; } = [];

    /// <summary>Accesses to the method's parameters.</summary>
    public IReadOnlyList<StateAccess> Parameters { get; private init; } = [];

    /// <summary>Uses of the clock, environment, console input, file reads and random sources.</summary>
    public IReadOnlyList<Finding> AmbientInputs { get; private init; } = [];

    /// <summary>Uses of the console, debug trace, file writes and environment changes.</summary>
    public IReadOnlyList<Finding> AmbientOutputs { get; private init; } = [];

    /// <summary>The method's effects, analyzed once per analysis.</summary>
    public static MethodEffects For(RuleContext ctx, MethodModel method) =>
        ctx.Facts.Get(method, () => Analyze(ctx, method));

    private static MethodEffects Analyze(RuleContext ctx, MethodModel method)
    {
        if (method.EffectiveBody is not { } body) return None;
        var names = StateInScope.For(ctx, method);
        var statics = new StateAccessCollector(names.Statics.Resolve);
        var instance = new StateAccessCollector(names.Instance.Resolve);
        var parameters = new StateAccessCollector(names.Parameters.Resolve);
        var ambient = new AmbientUses();
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
                return new StateInScope(NoStatics(""), NoNames, NoInstance, parameters);
            var state = ClassState.For(ctx, cls);
            var scopes = LexicalScopes.From(EnclosingMember(method.Node));
            var statics = method.IsStaticConstructor() ? NoStatics(cls.Name) : StateNames.StaticMembers(state.Statics, cls.Name, scopes);
            var instance = OwnsInstance(method) ? StateNames.InstanceMembers(state.Instance, scopes) : NoInstance;
            return new StateInScope(statics, state.MutableStatics, instance, parameters);
        }

        private static readonly StateNames NoInstance = StateNames.InstanceMembers(NoNames, LexicalScopes.None);

        private static StateNames NoStatics(string className) => StateNames.StaticMembers(NoNames, className, LexicalScopes.None);

        /// <summary>
        /// The member declaring the method: itself, or the property, indexer or
        /// method enclosing an accessor or local function, whose parameters and
        /// locals are visible inside it.
        /// </summary>
        private static SyntaxNode EnclosingMember(SyntaxNode node) =>
            node.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault() ?? node;

        /// <summary>Constructors set up the instance state and static members have none.</summary>
        private static bool OwnsInstance(MethodModel method) => !method.IsConstructor && !method.IsStatic();
    }
}
