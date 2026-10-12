using MessSharp.Model;
using MessSharp.Rule;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class EffectTransfer(
    RuleContext ctx,
    CallEdge edge,
    Func<EffectFact, IEnumerable<EffectFact>> mapUnknown)
{
    public IEnumerable<EffectFact> Map(EffectFact fact)
    {
        if (fact.Kind == EffectKind.Unknown) return mapUnknown(fact);
        if (fact.Kind is not EffectKind.ParameterChange and not EffectKind.ParameterWrite) return [fact];
        return MapParameterEffect(fact);
    }

    private IEnumerable<EffectFact> MapParameterEffect(EffectFact fact)
    {
        var targetParameter = edge.Target.Method.Parameters.FirstOrDefault(p => p.Name == fact.What);
        if (fact.Kind == EffectKind.ParameterWrite && targetParameter?.WritesReachCaller != true) return [];
        var actual = Actual(fact.What);
        if (actual is null) return [new EffectFact(EffectKind.Unknown, "unresolved argument binding")];
        var caller = edge.Caller;
        if (caller.Body is null) return [];
        var names = StateNames.Parameters(caller.Method.Parameters.Select(p => p.Name).ToHashSet());
        var storage = new ArgumentOwnership(caller.Body, names.Resolve).StorageAt(actual, edge.Call);
        var origins = fact.Reachable ? storage.Reachable : storage.Outer;
        var mapped = MapOrigins(fact, actual, origins);
        if (caller.Method.Class is { } cls)
            AddClassStateEffects(mapped, cls, actual);
        return mapped;
    }

    private static List<EffectFact> MapOrigins(EffectFact fact, ExpressionSyntax actual, IEnumerable<string> origins) =>
        origins.Select(name => new EffectFact(fact.Kind, name,
            fact.Reachable || actual is ElementAccessExpressionSyntax or MemberAccessExpressionSyntax)).ToList();

    private void AddClassStateEffects(List<EffectFact> mapped, ClassModel cls, ExpressionSyntax actual)
    {
        var state = ClassState.For(ctx, cls);
        AddState(mapped, StateNames.StaticMembers(state.Statics, cls.Name).Resolve, actual, EffectKind.Output, "static member");
        AddState(mapped, StateNames.InstanceMembers(state.Instance).Resolve, actual, EffectKind.InstanceOutput, "member");
    }

    private static void AddState(List<EffectFact> mapped, Func<ExpressionSyntax, string?> resolve,
        ExpressionSyntax actual, EffectKind kind, string noun)
    {
        var root = actual;
        while (root is ElementAccessExpressionSyntax element) root = element.Expression;
        if (resolve(root) is { } name)
            mapped.Add(new EffectFact(kind, $"changes the object in {noun} '{name}'"));
    }

    private ExpressionSyntax? Actual(string name)
    {
        var parameter = edge.Target.Method.Parameters.FindIndex(p => p.Name == name);
        var ownCount = edge.Target.Parent is null ? edge.Target.Method.Parameters.Count
            : CallableOwner.Parameters(edge.Target.Method.Node).Count();
        if (parameter >= ownCount)
            return SyntaxFactory.IdentifierName(name);
        var named = edge.Call.ArgumentList.Arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == name);
        if (named is not null) return named.Expression;
        return parameter >= 0 && parameter < edge.Call.ArgumentList.Arguments.Count
            ? edge.Call.ArgumentList.Arguments[parameter].Expression : null;
    }
}
