using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        foreach (var creation in BodyWalk.Nodes(body).OfType<ObjectCreationExpressionSyntax>())
        {
            var type = creation.Type.ToString();
            if (Trusted(owner, type)) continue;
            var name = type.Split('<')[0].Split('.').Last();
            var targets = owners.Where(o => o.Method.IsConstructor && !o.Method.IsStaticConstructor()
                && o.Method.Class?.Name == name && o.Method.Parameters.Count == (creation.ArgumentList?.Arguments.Count ?? 0))
                .Take(2).ToList();
            if (targets.Count == 1) AddEdge(owner, targets[0], creation, body);
            else summary.Add(new EffectFact(EffectKind.Unknown, "unresolved constructor '" + type + "'"), creation);
        }
    }

    private void AddEdge(CallableOwner owner, CallableOwner target, ObjectCreationExpressionSyntax creation, SyntaxNode body)
    {
        var call = SyntaxFactory.InvocationExpression(creation.Type, creation.ArgumentList ?? SyntaxFactory.ArgumentList());
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
