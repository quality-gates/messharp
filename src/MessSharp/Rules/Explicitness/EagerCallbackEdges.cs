using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class EagerCallbackEdges(RuleContext ctx, IReadOnlyList<CallableOwner> owners, List<CallEdge> edges)
{
    public void Add(CallableOwner owner, EffectSummary summary)
    {
        if (owner.Body is not { } body) return;
        foreach (var call in BodyWalk.Nodes(body).OfType<InvocationExpressionSyntax>())
            if (KnownExecutionOperations.IsEagerCallback(owner, call)) AddCall(owner, summary, call, body);
    }

    private void AddCall(CallableOwner owner, EffectSummary summary, InvocationExpressionSyntax call, SyntaxNode body)
    {
        var actual = call.ArgumentList.Arguments[0].Expression;
        var member = (MemberAccessExpressionSyntax)call.Expression;
        var argument = CallArguments.Ownership(ctx, owner, owners, body).StorageAt(member.Expression, call).Element();
        if (new CallbackResolver(owners).Find(owner, actual) is not { } callback)
        {
            var invocation = actual is IdentifierNameSyntax name && owner.Method.Parameters.Any(p => p.Name == name.Identifier.Text)
                ? new CallbackInvocation(name.Identifier.Text, call, [argument]) : null;
            summary.Add(new EffectFact(EffectKind.Unknown, "unresolved standard callback", Callback: invocation), call);
            return;
        }
        var count = callback.Parent is null ? callback.Method.Parameters.Count : CallableOwner.Parameters(callback.Method.Node).Count();
        var bindings = callback.Method.Parameters.Take(count).ToDictionary(parameter => parameter.Name, _ => argument);
        edges.Add(new CallEdge(owner, callback, call, call, bindings));
    }
}
