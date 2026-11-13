using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The type a call returns. A method that returns its own type parameter returns the type of the argument
/// it passes for a parameter declared with that type parameter; with no such parameter the type is unknown.
/// </summary>
internal static class GenericReturnTypes
{
    public static string? ReturnType(CallableOwner caller, CallableOwner target, InvocationExpressionSyntax call,
        Func<ExpressionSyntax, string?> typeOf)
    {
        var declared = target.Method.ReturnType;
        if (target.Method.Node is not MethodDeclarationSyntax method
            || method.TypeParameterList?.Parameters.Any(parameter => parameter.Identifier.Text == declared) != true)
            return declared;
        var index = target.Method.Parameters.FindIndex(parameter => parameter.Type == declared);
        if (index < 0) return null;
        var actual = CallArguments.Actual(target, call, target.Method.Parameters[index].Name, index);
        return actual is null || ReferenceEquals(caller, target) ? null : typeOf(actual);
    }
}
