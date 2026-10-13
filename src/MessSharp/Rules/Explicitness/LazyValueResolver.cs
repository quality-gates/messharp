using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class LazyValueResolver
{
    public static IEnumerable<DeferredWork> Resolve(IReadOnlyList<CallableOwner> owners,
        DeferredValueContext context, ExpressionSyntax value)
    {
        if (value is not BaseObjectCreationExpressionSyntax creation
            || !KnownExecutionOperations.IsLazy(context.Owner, creation)) return [];
        var callback = creation.ArgumentList is { } arguments
            ? CallArguments.Actual(arguments, "valueFactory", 0) : null;
        if (callback is null || new CallbackResolver(owners).Find(context.Owner, callback) is not { } target) return [];
        return [new DeferredWork(DeferredKind.Lazy, target,
            SyntaxFactory.InvocationExpression(callback), context.Captures(target))];
    }
}
