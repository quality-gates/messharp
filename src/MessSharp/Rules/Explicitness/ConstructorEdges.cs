using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ConstructorEdges(RuleContext ctx, IReadOnlyList<CallableOwner> owners, List<CallEdge> edges)
{
    private static readonly IReadOnlySet<string> TrustedTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Action", "Func", "Lazy", "Random", "DateTime", "DateTimeOffset", "TimeSpan", "Guid", "string", "String", "ArgumentException",
        "ArgumentOutOfRangeException", "InvalidOperationException", "Exception", "NotImplementedException", "ValueTuple",
    };
    public void Add(CallableOwner owner, EffectSummary summary)
    {
        var body = owner.Body;
        if (body is null) return;
        foreach (var creation in BodyWalk.Nodes(body).OfType<BaseObjectCreationExpressionSyntax>())
        {
            var type = new ExpressionTypes(owner).TypeOf(creation) ?? "<target-typed>";
            if (Trusted(owner, type) || ConstructorDefaults.IsEmpty(owner, type, creation)) continue;
            if (ConstructorCall.Resolve(owners, creation, type) is { } constructor)
                AddEdge(owner, constructor, creation, body);
            else summary.Add(new EffectFact(EffectKind.Unknown, "unresolved constructor '" + type + "'"), creation);
        }
    }

    private void AddEdge(CallableOwner owner, ConstructorCall constructor, BaseObjectCreationExpressionSyntax creation, SyntaxNode body)
    {
        var target = constructor.Target;
        var call = constructor.Invocation;
        var storage = CallArguments.Ownership(ctx, owner, owners, body);
        var bindings = target.Method.Parameters.Select((p, index) => (p.Name,
                Origin: storage.StorageAt(call.ArgumentList.Arguments[index].Expression, creation)))
            .ToDictionary(pair => pair.Name, pair => pair.Origin);
        edges.Add(new CallEdge(owner, target, call, creation, bindings));
    }

    private static bool Trusted(CallableOwner owner, string type)
    {
        if (KnownTypeChecks.IsCustomType(owner, type)) return false;
        var name = type.Split('<')[0].Split('.').Last();
        return StorageTypes.IsMutable(type) || TrustedTypes.Contains(name);
    }
}
