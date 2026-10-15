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
}
