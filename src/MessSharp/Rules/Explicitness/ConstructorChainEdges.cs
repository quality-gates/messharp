using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Work an instance constructor runs before its body: instance field initializers, then the
/// chained <c>this(...)</c> or base constructor. Same-file targets become edges; an explicit
/// chain the analysis cannot resolve is unknown. A creation of a class without constructors runs
/// the same work through its implicit constructor.
/// </summary>
internal sealed class ConstructorChainEdges(RuleContext ctx, IReadOnlyList<CallableOwner> owners, List<CallEdge> edges)
{
    public void Add(CallableOwner owner, EffectSummary summary)
    {
        if (ConstructorSite(owner, summary) is not { } site || owner.Method.Class is not { } cls) return;
        if (site.ChainsToThis) Chain(site, cls);
        else Construct(site, cls);
    }

    public void AddImplicit(CallableOwner owner, EffectSummary summary, ClassModel cls, BaseObjectCreationExpressionSyntax creation) =>
        Construct(ChainSite.ForImplicit(owner, summary, creation), cls);

    private static ChainSite? ConstructorSite(CallableOwner owner, EffectSummary summary) =>
        owner.Parent is null && owner.Method.Node is ConstructorDeclarationSyntax constructor
            && !owner.Method.IsStaticConstructor() ? ChainSite.ForConstructor(owner, summary, constructor) : null;

    private void Construct(ChainSite site, ClassModel cls)
    {
        if (!site.Visit(cls)) return;
        foreach (var initializer in owners.Where(o => o.Method.Class == cls && FieldInitializerOwners.IsInstance(o)))
            edges.Add(new CallEdge(site.Owner, initializer, ConstructorTypes.Invocation(cls.Name, null), site.InitializerAnchor));
        ChainBase(site, cls);
    }

    /// <summary>A same-file base class chains on; an explicit call to an untrusted external base is unknown.</summary>
    private void ChainBase(ChainSite site, ClassModel cls)
    {
        if (ConstructorTypes.BaseClass(cls) is { } baseClass)
        {
            Chain(site, baseClass);
            return;
        }
        var type = cls.BaseTypes.FirstOrDefault();
        if (site.CallsBase(type) && !ConstructorTypes.Trusted(site.Owner, type ?? ""))
            site.Unknown(type ?? "base");
    }

    private void Chain(ChainSite site, ClassModel cls)
    {
        var arguments = site.Arguments;
        var targets = owners.Where(o => o.Parent is null && o != site.Owner && o.Method.Class == cls
            && o.Method.IsConstructor && !o.Method.IsStaticConstructor()).ToList();
        var matching = targets.Where(o => o.Method.Parameters.Count == arguments.Arguments.Count).Take(2).ToList();
        if (matching.Count == 1) AddEdge(site, matching[0], cls, arguments);
        else if (targets.Count == 0 && arguments.Arguments.Count == 0) Construct(site.WithoutInitializer(), cls);
        else site.Unknown(cls.Name);
    }

    private void AddEdge(ChainSite site, CallableOwner target, ClassModel cls, ArgumentListSyntax arguments)
    {
        var storage = CallArguments.Ownership(ctx, site.Owner, owners, site.Owner.Body ?? site.Declaration);
        var bindings = target.Method.Parameters.Select((parameter, index) => (parameter.Name,
                Actual: CallArguments.Actual(arguments, parameter.Name, index)))
            .ToDictionary(pair => pair.Name, pair => pair.Actual is null ? StorageOrigin.Unknown
                : storage.StorageAt(pair.Actual, site.Declaration));
        edges.Add(new CallEdge(site.Owner, target, ConstructorTypes.Invocation(cls.Name, arguments), site.Anchor, bindings));
    }

    /// <summary>
    /// Where construction work starts: <see cref="Declaration"/> runs the class's own initializers,
    /// <see cref="Anchor"/> is the chained call into the next constructor.
    /// </summary>
    private sealed record ChainSite(CallableOwner Owner, EffectSummary Summary, SyntaxNode Declaration, SyntaxNode Anchor,
        SyntaxNode InitializerAnchor, ConstructorInitializerSyntax? Initializer, HashSet<ClassModel> Visited,
        bool Implicit = false)
    {
        public bool ChainsToThis => Initializer?.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword) == true;

        public ArgumentListSyntax Arguments => Initializer?.ArgumentList ?? SyntaxFactory.ArgumentList();

        public static ChainSite ForConstructor(CallableOwner owner, EffectSummary summary,
            ConstructorDeclarationSyntax constructor) => new(owner, summary, constructor,
            constructor.Initializer ?? (SyntaxNode)constructor, constructor, constructor.Initializer, []);

        public static ChainSite ForImplicit(CallableOwner owner, EffectSummary summary,
            BaseObjectCreationExpressionSyntax creation) =>
            new(owner, summary, creation, creation, creation, null, [], true);

        public bool Visit(ClassModel cls) => Visited.Add(cls);

        public bool CallsBase(string? type) => Initializer is not null || (Implicit && type is not null);

        public ChainSite WithoutInitializer() => this with { Initializer = null, InitializerAnchor = Anchor };

        public void Unknown(string type) =>
            Summary.Add(new EffectFact(EffectKind.Unknown, "unresolved constructor '" + type + "'"), Anchor);
    }
}

internal static class ConstructorTypes
{
    private static readonly IReadOnlySet<string> TrustedTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Action", "Func", "Lazy", "Random", "DateTime", "DateTimeOffset", "TimeSpan", "Guid", "string", "String", "ArgumentException",
        "ArgumentOutOfRangeException", "InvalidOperationException", "Exception", "NotImplementedException", "ValueTuple",
    };

    public static InvocationExpressionSyntax Invocation(string type, ArgumentListSyntax? arguments) =>
        SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(type), arguments ?? SyntaxFactory.ArgumentList());

    public static bool Trusted(CallableOwner owner, string type)
    {
        if (KnownTypeChecks.IsCustomType(owner, type)) return false;
        var name = type.Split('<')[0].Split('.').Last();
        return StorageTypes.IsMutable(type) || TrustedTypes.Contains(name);
    }

    public static ClassModel? BaseClass(ClassModel cls) => cls.BaseTypes.FirstOrDefault() is { } type
        ? cls.File.Classes.FirstOrDefault(candidate => candidate.Name == type.Split('<')[0].Split('.').Last()
            && candidate.Node is ClassDeclarationSyntax)
        : null;

    /// <summary>The single same-file class a parameterless creation builds through its implicit constructor.</summary>
    public static ClassModel? ImplicitlyConstructed(CallableOwner owner, string type, BaseObjectCreationExpressionSyntax creation)
    {
        if (creation.ArgumentList?.Arguments.Count > 0) return null;
        var name = type.Split('<')[0].Split('.').Last();
        var classes = owner.Method.File.Classes.Where(cls => cls.Name == name).Take(2).ToList();
        return classes is [{ Node: ClassDeclarationSyntax { ParameterList: null } declaration } cls]
            && !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            && !declaration.Members.OfType<ConstructorDeclarationSyntax>().Any(ctor => !ctor.Modifiers.Any(SyntaxKind.StaticKeyword))
            ? cls : null;
    }
}
