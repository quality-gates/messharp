using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The <c>foreach</c> iteration variables: where a name is declared, and the type it is declared with.</summary>
internal static class LoopVariables
{
    /// <summary>The innermost <c>foreach</c> whose iteration variable <paramref name="name"/> reads.</summary>
    public static ForEachStatementSyntax? Find(IdentifierNameSyntax name) => name.Ancestors()
        .OfType<ForEachStatementSyntax>()
        .FirstOrDefault(loop => loop.Identifier.Text == name.Identifier.Text && loop.Statement.Span.Contains(name.Span));

    /// <summary>The declared type, or for <c>var</c> the element type of the iterated expression.</summary>
    public static string? TypeOf(ForEachStatementSyntax loop, Func<ExpressionSyntax, string?> typeOf,
        Func<string?, string?> element) =>
        loop.Type.IsVar ? element(typeOf(loop.Expression)) : loop.Type.ToString();
}
