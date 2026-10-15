using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The call work of a delegate value that names its callable: a lambda, an anonymous method or a method group.</summary>
internal static class CallableWork
{
    private static readonly DeferredResolution Unknown = new([], false);

    public static DeferredResolution Creation(IReadOnlyList<CallableOwner> owners, DeferredValueContext context,
        ExpressionSyntax value) =>
        CallableNodeLookup.Find(owners, value) is { } target ? Call(context, target, value) : Unknown;

    public static DeferredResolution Group(IReadOnlyList<CallableOwner> owners, DeferredValueContext context,
        ExpressionSyntax value, int arity) =>
        new CallableLookup(owners).Group(context.Owner, value, arity) is { } target ? Call(context, target, value) : Unknown;

    private static DeferredResolution Call(DeferredValueContext context, CallableOwner target, ExpressionSyntax value) =>
        new([new DeferredWork(DeferredKind.Call, target, SyntaxFactory.InvocationExpression(value), context.Captures(target))],
            true);
}
