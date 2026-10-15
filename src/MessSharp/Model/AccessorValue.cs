using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Model;

/// <summary>
/// The implicit <c>value</c> parameter of a set, init, add or remove accessor. The source declares no node for it,
/// so each accessor gets one synthetic node that every reader shares.
/// </summary>
internal static class AccessorValue
{
    private static readonly ConditionalWeakTable<AccessorDeclarationSyntax, ParameterSyntax> Parameters = new();

    public static bool Takes(AccessorDeclarationSyntax accessor) => accessor.Keyword.Kind() is SyntaxKind.SetKeyword
        or SyntaxKind.InitKeyword or SyntaxKind.AddKeyword or SyntaxKind.RemoveKeyword;

    public static ParameterSyntax Parameter(AccessorDeclarationSyntax accessor) =>
        Parameters.GetValue(accessor, _ => SyntaxFactory.Parameter(SyntaxFactory.Identifier("value")));
}
