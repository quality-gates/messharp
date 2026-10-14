using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class DelegateCallee
{
    /// <summary>
    /// The delegate value an invocation runs: the receiver of <c>Invoke</c> or <c>?.Invoke</c>, otherwise the
    /// callee itself. Null when the callee is a conditional member other than <c>Invoke</c>.
    /// </summary>
    public static ExpressionSyntax? Value(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax { Name.Identifier.Text: "Invoke" } member => member.Expression,
        MemberBindingExpressionSyntax { Name.Identifier.Text: "Invoke" } =>
            (call.Parent as ConditionalAccessExpressionSyntax)?.Expression,
        MemberBindingExpressionSyntax => null,
        var callee => callee,
    };
}
