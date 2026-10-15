using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class CallableNodeLookup
{
    public static CallableOwner? Find(IReadOnlyList<CallableOwner> owners, ExpressionSyntax expression) =>
        owners.FirstOrDefault(o => o.Method.Node == CallableSyntax.Unwrap(expression));
}
