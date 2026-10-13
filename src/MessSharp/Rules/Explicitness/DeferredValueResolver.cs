using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal enum DeferredKind { Sequence, Lazy }

internal sealed record DeferredWork(DeferredKind Kind, CallableOwner Target, InvocationExpressionSyntax Call,
    IReadOnlyDictionary<string, StorageOrigin> Bindings);

internal sealed class DeferredValueResolver(RuleContext ctx, IReadOnlyList<CallableOwner> owners)
{
    public IEnumerable<DeferredWork> Resolve(CallableOwner caller, ExpressionSyntax value, SyntaxNode body) =>
        Resolve(new DeferredValueContext(ctx, owners, caller, body), value, new HashSet<SyntaxNode>());

    private IEnumerable<DeferredWork> Resolve(DeferredValueContext context, ExpressionSyntax value, HashSet<SyntaxNode> visiting)
    {
        value = CallableSyntax.Unwrap(value);
        if (!visiting.Add(value)) return [];
        try { return ResolveValue(context, value, visiting).ToList(); }
        finally { visiting.Remove(value); }
    }

    private IEnumerable<DeferredWork> ResolveValue(DeferredValueContext context, ExpressionSyntax value, HashSet<SyntaxNode> visiting) => value switch
    {
        IdentifierNameSyntax or MemberAccessExpressionSyntax =>
            context.Values(value).SelectMany(bound => Resolve(context, bound, visiting)),
        InvocationExpressionSyntax call => ResolveCall(context, call, visiting),
        ConditionalExpressionSyntax choice => Resolve(context, choice.WhenTrue, visiting)
            .Concat(Resolve(context, choice.WhenFalse, visiting)),
        _ => LazyValueResolver.Resolve(owners, context, value),
    };

    private IEnumerable<DeferredWork> ResolveCall(DeferredValueContext context, InvocationExpressionSyntax call, HashSet<SyntaxNode> visiting)
    {
        if (KnownExecutionOperations.IsColdQuery(context.Owner, call)) return QueryWork(context, call, visiting);
        var target = new CallResolver(owners).Resolve(context.Owner, call);
        if (target?.Body is not { } body) return [];
        var bound = context.Bind(target, call, body);
        if (KnownExecutionOperations.IsIterator(target)) return [new DeferredWork(DeferredKind.Sequence, target, call, bound.Bindings)];
        return Returns(body).SelectMany(returned => Resolve(bound, returned, visiting));
    }

    private IEnumerable<DeferredWork> QueryWork(DeferredValueContext context, InvocationExpressionSyntax call, HashSet<SyntaxNode> visiting)
    {
        var member = (MemberAccessExpressionSyntax)call.Expression;
        var source = Resolve(context, member.Expression, visiting).ToList();
        if (call.ArgumentList.Arguments.FirstOrDefault()?.Expression is not { } callback) return source;
        var target = new CallbackResolver(owners).Find(context.Owner, callback);
        if (target is null) return source;
        var bindings = context.Captures(target);
        var parameter = CallableOwner.Parameters(target.Method.Node).FirstOrDefault();
        if (parameter is not null) bindings[parameter.Identifier.Text] = context.Storage(member.Expression).Element();
        source.Add(new DeferredWork(DeferredKind.Sequence, target, call, bindings));
        return source;
    }

    private static IEnumerable<ExpressionSyntax> Returns(SyntaxNode body) => body is ExpressionSyntax expression ? [expression]
        : BodyWalk.Nodes(body).OfType<ReturnStatementSyntax>().Select(r => r.Expression).OfType<ExpressionSyntax>();
}
