using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class LazyValueResolver
{
    public static IEnumerable<DeferredWork> Resolve(IReadOnlyList<CallableOwner> owners,
        DeferredValueContext context, ExpressionSyntax value)
    {
        if (value is not ObjectCreationExpressionSyntax creation
            || !KnownExecutionOperations.IsLazy(context.Owner, creation)) return [];
        var callback = creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
        var target = callback is null ? null : new CallbackResolver(owners).Find(context.Owner, callback);
        return target is null ? [] : [new DeferredWork(DeferredKind.Lazy, target,
            SyntaxFactory.InvocationExpression(creation.Type), context.Captures(target))];
    }
}
