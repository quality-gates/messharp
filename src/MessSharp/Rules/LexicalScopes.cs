using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules;

/// <summary>
/// Syntax-only lexical-scope index: maps the names of parameters and locals
/// declared under a root to the nodes whose span they are visible in. A bare
/// identifier bound to such a declaration can never refer to a member of the
/// containing type, so member-usage rules must not count it as one.
/// </summary>
internal sealed class LexicalScopes
{
    /// <summary>An index that shadows nothing.</summary>
    public static readonly LexicalScopes None = new(new Dictionary<string, List<SyntaxNode>>());

    private readonly Dictionary<string, List<SyntaxNode>> _scopes;

    private LexicalScopes(Dictionary<string, List<SyntaxNode>> scopes) => _scopes = scopes;

    /// <summary>Indexes every local declaration under <paramref name="root"/>.</summary>
    public static LexicalScopes From(SyntaxNode root)
    {
        var scopes = new Dictionary<string, List<SyntaxNode>>(StringComparer.Ordinal);
        foreach (var declaration in root.DescendantNodes().Select(LocalDeclaration.For))
        {
            if (declaration is not { } d) continue;
            if (!scopes.TryGetValue(d.Name, out var list))
                scopes[d.Name] = list = new List<SyntaxNode>();
            list.AddRange(d.Scopes);
        }
        return new LexicalScopes(scopes);
    }

    /// <summary>
    /// True when the identifier is lexically bound to a local variable or
    /// parameter declared in an enclosing scope.
    /// </summary>
    public bool IsShadowed(IdentifierNameSyntax identifier) =>
        _scopes.TryGetValue(identifier.Identifier.Text, out var scopes)
        && scopes.Any(scope => scope.Span.Contains(identifier.Span));
}
