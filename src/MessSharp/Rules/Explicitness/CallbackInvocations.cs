using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class CallbackInvocations
{
    public static IEnumerable<EffectFact> Forward(EffectFact fact, CallbackInvocation invocation,
        ExpressionSyntax actual, IReadOnlyList<StorageOrigin> arguments) => actual is IdentifierNameSyntax name
        ? [fact with { Callback = new CallbackInvocation(name.Identifier.Text, invocation.Call, arguments) }]
        : [fact with { Callback = null }];

    public static CallbackInvocation? From(RuleContext ctx, CallableOwner owner,
        IReadOnlyList<CallableOwner> owners, InvocationExpressionSyntax call, SyntaxNode body)
    {
        if (call.Expression is not IdentifierNameSyntax name
            || !owner.Method.Parameters.Any(p => p.Name == name.Identifier.Text)) return null;
        var ownership = CallArguments.Ownership(ctx, owner, owners, body);
        return new CallbackInvocation(name.Identifier.Text, call,
            call.ArgumentList.Arguments.Select(argument => ownership.StorageAt(argument.Expression, call)).ToList());
    }
}
