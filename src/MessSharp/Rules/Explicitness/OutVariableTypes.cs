using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The type of an <c>out var</c> declaration: the type of the parameter the resolved callee declares.</summary>
internal static class OutVariableTypes
{
    public static string? Find(CallableOwner owner, IReadOnlyList<CallableOwner> owners, SingleVariableDesignationSyntax designation)
    {
        if (designation.Parent is not DeclarationExpressionSyntax { Type.IsVar: true, Parent: ArgumentSyntax argument }
            || argument.Parent?.Parent is not InvocationExpressionSyntax call
            || new CallResolver(owners).Resolve(owner, call) is not { } target) return null;
        return DeclaredParameters.Bound(target, call, argument)?.Type?.ToString();
    }
}
