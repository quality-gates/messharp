using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>Static methods declared with a <c>this</c> first parameter, callable as receiver members.</summary>
internal static class ExtensionMethods
{
    public static bool IsExtension(CallableOwner owner) => owner.Parent is null && owner.Method.Node is MethodDeclarationSyntax method
        && method.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(SyntaxKind.ThisKeyword) == true;

    /// <summary>The receiver type the extension's first parameter declares.</summary>
    public static string? ReceiverType(CallableOwner owner) => owner.Method.Node is MethodDeclarationSyntax method
        ? method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString() : null;

    /// <summary>Whether the extension method takes a receiver of type <paramref name="receiver"/>.</summary>
    public static bool Binds(CallableOwner owner, string receiver) => IsExtension(owner)
        && (IsOpenReceiver(owner)
            || ReceiverType(owner) is { } declared && TypeNames.Simple(declared) == TypeNames.Simple(receiver));

    /// <summary>True when the extension's receiver is an unconstrained type parameter of its own, so it binds any receiver type.</summary>
    public static bool IsOpenReceiver(CallableOwner owner) => owner.Method.Node is MethodDeclarationSyntax method
        && method.ParameterList.Parameters.FirstOrDefault()?.Type is IdentifierNameSyntax receiver
        && method.TypeParameterList?.Parameters.Any(parameter => parameter.Identifier.Text == receiver.Identifier.Text) == true
        && method.ConstraintClauses.All(clause => clause.Name.Identifier.Text != receiver.Identifier.Text);
}
