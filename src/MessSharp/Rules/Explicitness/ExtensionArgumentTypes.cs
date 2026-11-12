using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class ExtensionArgumentTypes
{
    public static ArgumentCompatibility Classify(CallableOwner caller, CallableOwner candidate, InvocationExpressionSyntax call)
    {
        var parameters = candidate.Method.Parameters.Skip(1).ToList();
        var uncertain = caller.Method.Namespace != candidate.Method.Namespace || !ExactReceiver(caller, candidate, call);
        var index = 0;
        foreach (var argument in call.ArgumentList.Arguments)
        {
            var parameter = argument.NameColon is { } named
                ? parameters.FirstOrDefault(p => p.Name == named.Name.Identifier.Text)
                : parameters.ElementAtOrDefault(index);
            index++;
            if (parameter is null || parameter.Node.Modifiers.Any(SyntaxKind.ParamsKeyword))
            {
                uncertain = true;
                continue;
            }
            var match = PrimitiveConversions.Compare(caller, candidate, argument.Expression, parameter.Type);
            if (match == ArgumentCompatibility.Incompatible) return match;
            uncertain |= match == ArgumentCompatibility.Uncertain;
        }
        return uncertain ? ArgumentCompatibility.Uncertain : ArgumentCompatibility.Exact;
    }

    private static bool ExactReceiver(CallableOwner caller, CallableOwner candidate, InvocationExpressionSyntax call)
    {
        if (call.Expression is not MemberAccessExpressionSyntax member || ExtensionMethods.ReceiverType(candidate) is not { } declared)
            return false;
        var actual = new ExpressionTypes(caller).TypeOf(member.Expression);
        if (actual is null) return false;
        actual = SequenceTypeAliases.Resolve(caller, actual).Replace(" ", "", StringComparison.Ordinal);
        declared = SequenceTypeAliases.Resolve(candidate, declared).Replace(" ", "", StringComparison.Ordinal);
        var actualArguments = actual.IndexOf('<');
        var declaredArguments = declared.IndexOf('<');
        return TypeNames.Simple(actual) == TypeNames.Simple(declared)
            && actualArguments >= 0 && declaredArguments >= 0
            && actual[actualArguments..] == declared[declaredArguments..];
    }
}
