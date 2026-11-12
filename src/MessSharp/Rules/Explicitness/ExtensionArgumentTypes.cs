using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class ExtensionArgumentTypes
{
    public static bool CouldMatch(CallableOwner caller, CallableOwner candidate, InvocationExpressionSyntax call)
    {
        var parameters = candidate.Method.Parameters.Skip(1).ToList();
        var types = new ExpressionTypes(caller);
        var index = 0;
        foreach (var argument in call.ArgumentList.Arguments)
        {
            var parameter = argument.NameColon is { } named
                ? parameters.FirstOrDefault(p => p.Name == named.Name.Identifier.Text)
                : parameters.ElementAtOrDefault(index);
            index++;
            if (parameter is not null && !PrimitiveConversions.CouldConvert(caller, types.TypeOf(argument.Expression), parameter.Type))
                return false;
        }
        return true;
    }
}
