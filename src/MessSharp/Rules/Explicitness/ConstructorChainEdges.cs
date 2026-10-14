using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Work an instance constructor runs before its body: instance field initializers, then the
/// chained <c>this(...)</c> or base constructor. Same-file targets become edges; an explicit
/// chain the analysis cannot resolve is unknown.
/// </summary>
internal sealed class ConstructorChainEdges(RuleContext ctx, IReadOnlyList<CallableOwner> owners, List<CallEdge> edges)
{
    public void Add(CallableOwner owner, EffectSummary summary)
    {
        if (owner.Parent is not null || owner.Method.Node is not ConstructorDeclarationSyntax constructor
            || owner.Method.IsStaticConstructor() || owner.Method.Class is not { } cls) return;
        var site = new ChainSite(owner, summary, constructor, constructor.Initializer);
        if (constructor.Initializer?.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword) == true)
        {
            Chain(site, cls, new HashSet<ClassModel>());
            return;
        }
        Construct(site, cls, new HashSet<ClassModel>());
    }

    private void Construct(ChainSite site, ClassModel cls, HashSet<ClassModel> visiting)
    {
        if (!visiting.Add(cls)) return;
        foreach (var initializer in owners.Where(o => o.Method.Class == cls && FieldInitializerOwners.IsInstance(o)))
            edges.Add(new CallEdge(site.Owner, initializer, Invocation(cls.Name, null), site.Constructor));
        ChainBase(site, cls, visiting);
    }

    /// <summary>A same-file base class chains on; an explicit call to an untrusted external base is unknown.</summary>
    private void ChainBase(ChainSite site, ClassModel cls, HashSet<ClassModel> visiting)
    {
        if (ConstructorTypes.BaseClass(cls) is { } baseClass)
        {
            Chain(site, baseClass, visiting);
            return;
        }
        var type = cls.BaseTypes.FirstOrDefault();
        if (site.Initializer is not null && !ConstructorTypes.Trusted(site.Owner, type ?? "")) site.Unknown(type ?? "base");
    }

    private void Chain(ChainSite site, ClassModel cls, HashSet<ClassModel> visiting)
    {
        var arguments = site.Initializer?.ArgumentList ?? SyntaxFactory.ArgumentList();
        var targets = owners.Where(o => o.Parent is null && o != site.Owner && o.Method.Class == cls
            && o.Method.IsConstructor && !o.Method.IsStaticConstructor()).ToList();
        var matching = targets.Where(o => o.Method.Parameters.Count == arguments.Arguments.Count).Take(2).ToList();
        if (matching.Count == 1) AddEdge(site, matching[0], cls, arguments);
        else if (targets.Count == 0 && arguments.Arguments.Count == 0) Construct(site with { Initializer = null }, cls, visiting);
        else site.Unknown(cls.Name);
    }

    private void AddEdge(ChainSite site, CallableOwner target, ClassModel cls, ArgumentListSyntax arguments)
    {
        var storage = CallArguments.Ownership(ctx, site.Owner, owners, site.Owner.Body ?? site.Constructor);
        var bindings = target.Method.Parameters.Select((parameter, index) => (parameter.Name,
                Actual: CallArguments.Actual(arguments, parameter.Name, index)))
            .ToDictionary(pair => pair.Name, pair => pair.Actual is null ? StorageOrigin.Unknown
                : storage.StorageAt(pair.Actual, site.Constructor));
        edges.Add(new CallEdge(site.Owner, target, Invocation(cls.Name, arguments), site.Constructor, bindings));
    }

    private static InvocationExpressionSyntax Invocation(string type, ArgumentListSyntax? arguments) =>
        SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(type), arguments ?? SyntaxFactory.ArgumentList());

    private sealed record ChainSite(CallableOwner Owner, EffectSummary Summary, ConstructorDeclarationSyntax Constructor,
        ConstructorInitializerSyntax? Initializer)
    {
        public void Unknown(string type) =>
            Summary.Add(new EffectFact(EffectKind.Unknown, "unresolved constructor '" + type + "'"), Constructor);
    }
}

internal static class ConstructorTypes
{
    private static readonly IReadOnlySet<string> TrustedTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Action", "Func", "Lazy", "Random", "DateTime", "DateTimeOffset", "TimeSpan", "Guid", "string", "String", "ArgumentException",
        "ArgumentOutOfRangeException", "InvalidOperationException", "Exception", "NotImplementedException", "ValueTuple",
    };

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
}
