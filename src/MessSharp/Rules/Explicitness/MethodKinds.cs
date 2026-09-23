using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class MethodKinds
{
    /// <summary>
    /// True for a static constructor. It runs once to set up the static
    /// state, so its writes to that state are its purpose, not a side effect.
    /// </summary>
    public static bool IsStaticConstructor(this MethodModel method) =>
        method.Node is ConstructorDeclarationSyntax ctor && ctor.Modifiers.Any(SyntaxKind.StaticKeyword);

    /// <summary>True for a static member; accessors take the modifiers of their property.</summary>
    public static bool IsStatic(this MethodModel method) => method.Node switch
    {
        MemberDeclarationSyntax member => member.Modifiers.Any(SyntaxKind.StaticKeyword),
        AccessorDeclarationSyntax accessor =>
            accessor.FirstAncestorOrSelf<BasePropertyDeclarationSyntax>()?.Modifiers.Any(SyntaxKind.StaticKeyword) == true,
        _ => false,
    };
}
