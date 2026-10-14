using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The parameters a callable declares and the parameter each call argument binds.</summary>
internal static class DeclaredParameters
{
    public static IEnumerable<ParameterSyntax> Declared(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
        IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters,
        _ => CallableOwner.Parameters(node),
    };

    /// <summary>The parameter of <paramref name="target"/> that <paramref name="argument"/> of <paramref name="call"/> binds.</summary>
    public static ParameterSyntax? Bound(CallableOwner target, InvocationExpressionSyntax call, ArgumentSyntax argument)
    {
        var parameters = Declared(target.Method.Node).ToList();
        if (argument.NameColon is { } named) return parameters.FirstOrDefault(p => p.Identifier.Text == named.Name.Identifier.Text);
        var index = call.ArgumentList.Arguments.IndexOf(argument) + (CallArguments.IsReducedExtension(target, call) ? 1 : 0);
        return index >= 0 && index < parameters.Count ? parameters[index] : null;
    }
}
