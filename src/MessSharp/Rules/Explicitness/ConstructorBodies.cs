using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The executable extent of a member. A constructor initializer's arguments run inside the
/// constructor, so a constructor with arguments in <c>: base(...)</c> or <c>: this(...)</c> owns its whole declaration.
/// </summary>
internal static class ConstructorBodies
{
    public static SyntaxNode? BodyOf(MethodModel method) =>
        method.Node is ConstructorDeclarationSyntax { Initializer.ArgumentList.Arguments.Count: > 0 } constructor
            ? constructor : method.EffectiveBody;

    /// <summary>Declaration parts of a constructor root that do not execute.</summary>
    public static IEnumerable<SyntaxNode> Inert(ConstructorDeclarationSyntax constructor) =>
        constructor.AttributeLists.Cast<SyntaxNode>().Append(constructor.ParameterList);
}
