using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class LazyValueResolver
{
    /// <summary>
    /// The work forcing a Lazy creation runs, or null when <paramref name="value"/> is not one. A creation
    /// without a factory constructs its value when forced; an unresolvable factory stays incomplete.
    /// </summary>
    public static DeferredResolution? Resolve(IReadOnlyList<CallableOwner> owners,
        DeferredValueContext context, ExpressionSyntax value)
    {
        if (value is not BaseObjectCreationExpressionSyntax creation
            || !KnownExecutionOperations.IsLazy(context.Owner, creation)) return null;
        var factory = LazyFactories.Select(context.Owner, creation);
        return factory.Kind switch
        {
            LazyFactoryKind.None => new DeferredResolution([], true),
            LazyFactoryKind.Default => LazyDefaultConstruction.Resolve(owners, context, creation),
            LazyFactoryKind.Factory => ResolveFactory(owners, context, factory.Expression),
            _ => new DeferredResolution([], false),
        };
    }

    private static DeferredResolution ResolveFactory(IReadOnlyList<CallableOwner> owners,
        DeferredValueContext context, ExpressionSyntax? expression)
    {
        if (expression is not { } callback
            || new CallbackResolver(owners).Find(context.Owner, callback) is not { } target)
            return new DeferredResolution([], false);
        return new DeferredResolution([new DeferredWork(DeferredKind.Lazy, target,
            SyntaxFactory.InvocationExpression(callback), context.Captures(target))], true);
    }
}
