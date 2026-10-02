using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MessSharp.Model;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// The scope of a class or struct primary constructor's parameters: they are
/// captured by the whole type declaration rather than a body (issue #199).
/// </summary>
internal static class PrimaryConstructorScope
{
    /// <summary>
    /// Returns the declaring type when <paramref name="method"/> is a primary
    /// constructor whose parameters can be checked for use. Positional records
    /// are excluded (their parameters are public state), as are partial types
    /// (another part, possibly in another file, may read the parameter).
    /// </summary>
    internal static TypeDeclarationSyntax? CheckableType(MethodModel method)
    {
        if (method.IsPositionalRecordConstructor) return null;
        if (method.Node is not ParameterListSyntax { Parent: TypeDeclarationSyntax type }) return null;
        return type.Modifiers.Any(SyntaxKind.PartialKeyword) ? null : type;
    }

    /// <summary>
    /// Identifier reads that can bind to a primary constructor parameter: those
    /// in the base list (<c>: Base(x)</c>) and in the type's members, excluding
    /// nested types (which cannot capture the parameters) and reads bound to a
    /// member's own same-named parameter or local.
    /// </summary>
    internal static HashSet<string> Reads(TypeDeclarationSyntax type)
    {
        var reads = new HashSet<string>(StringComparer.Ordinal);
        if (type.BaseList != null)
            reads.UnionWith(BodyAnalysis.IdentReads(type.BaseList));

        foreach (var member in type.Members.Where(m => m is not BaseTypeDeclarationSyntax))
            reads.UnionWith(UnshadowedReads(member));

        return reads;
    }

    private static HashSet<string> UnshadowedReads(SyntaxNode member)
    {
        var shadows = LexicalScopes.From(member);
        return BodyAnalysis.IdentReads(member, shadows.IsShadowed);
    }
}
