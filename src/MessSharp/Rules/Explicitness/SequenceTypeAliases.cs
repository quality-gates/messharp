using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class SequenceTypeAliases
{
    public static string Resolve(CallableOwner owner, string type) =>
        owner.Method.Node.Ancestors().SelectMany(Usings)
            .FirstOrDefault(usingDirective => usingDirective.Alias?.Name.Identifier.Text == type)
            ?.Name?.ToString() ?? type;

    private static IEnumerable<UsingDirectiveSyntax> Usings(SyntaxNode scope) => scope switch
    {
        CompilationUnitSyntax unit => unit.Usings,
        BaseNamespaceDeclarationSyntax declaration => declaration.Usings,
        _ => [],
    };
}
