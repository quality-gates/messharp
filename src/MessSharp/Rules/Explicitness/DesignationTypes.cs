using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>Out variables and pattern variables: where a name is declared, and the type it is declared with.</summary>
internal static class DesignationTypes
{
    public static SingleVariableDesignationSyntax? Declaration(IdentifierNameSyntax name) => name.Ancestors()
        .OfType<BlockSyntax>()
        .Select(block => block.DescendantNodes().OfType<SingleVariableDesignationSyntax>().LastOrDefault(designation =>
            designation.Identifier.Text == name.Identifier.Text && designation.SpanStart < name.SpanStart
            && designation.Ancestors().OfType<BlockSyntax>().FirstOrDefault() == block))
        .FirstOrDefault(designation => designation is not null);

    public static string? Find(IdentifierNameSyntax name) => Declaration(name)?.Parent switch
    {
        DeclarationExpressionSyntax { Type.IsVar: false } declared => declared.Type.ToString(),
        DeclarationPatternSyntax pattern => pattern.Type.ToString(),
        RecursivePatternSyntax { Type: { } type } => type.ToString(),
        _ => null,
    };
}
