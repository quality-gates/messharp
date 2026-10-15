using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class LazyValueResolver
{
    /// <summary>
    /// The work forcing a Lazy creation runs, or null when <paramref name="value"/> is not one. A creation
    /// without a factory constructs its value when forced; an unresolvable factory stays incomplete.
    /// </summary>
    public static DeferredResolution? Resolve(IReadOnlyList<CallableOwner> owners,
        DeferredValueContext context, ExpressionSyntax value,
        Func<DeferredValueContext, ExpressionSyntax, int, DeferredResolution> callables)
    {
        if (value is not BaseObjectCreationExpressionSyntax creation
            || !KnownExecutionOperations.IsLazy(context.Owner, creation)) return null;
        var factory = LazyFactories.Select(context.Owner, creation);
        return factory.Kind switch
        {
            LazyFactoryKind.None => new DeferredResolution([], true),
            LazyFactoryKind.Default => LazyDefaultConstruction.Resolve(owners, context, creation),
            LazyFactoryKind.Factory when factory.Expression is { } callback => ResolveFactory(context, callback, callables),
            _ => new DeferredResolution([], false),
        };
    }

    /// <summary>Every callable the factory can hold runs when the value is forced; a factory parameter stays unknown.</summary>
    private static DeferredResolution ResolveFactory(DeferredValueContext context, ExpressionSyntax callback,
        Func<DeferredValueContext, ExpressionSyntax, int, DeferredResolution> callables)
    {
        var held = callables(context, callback, 0);
        return new DeferredResolution(held.Work.Select(work => work with { Kind = DeferredKind.Lazy }).ToList(), held.Resolved);
    }
}
