using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The values a callee can leave in an argument, and the bound callee frame that evaluates them.</summary>
internal sealed record DeferredExit(DeferredValueContext Context, IReadOnlyList<DeferredSource> Sources);

/// <summary>Follows an <c>out</c> or <c>ref</c> argument into the resolved callee that writes it.</summary>
internal sealed class DeferredArgumentExits(IReadOnlyList<CallableOwner> owners)
{
    public DeferredExit? Find(DeferredValueContext context, ArgumentSyntax argument)
    {
        if (argument.Parent?.Parent is not InvocationExpressionSyntax call
            || new CallResolver(owners).Resolve(context.Owner, call) is not { Body: { } body } target
            || DeclaredParameters.Bound(target, call, argument) is not { } parameter) return null;
        var sources = new DeclarationFlow(body, new DeferredDeclarations(target)).Exit(parameter);
        return new DeferredExit(context.Bind(target, call, body), sources);
    }
}
