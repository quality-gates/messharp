using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// Maps locally declared names (parameters and local variables) to the
/// syntax nodes whose span they are visible in. A bare identifier bound
/// to such a declaration can never refer to a class member, so usage
/// rules must not count it as member usage (issue #90: shadowing).
/// </summary>
internal sealed class ShadowMap
{
    private readonly Dictionary<string, List<SyntaxNode>> _declarations;

    private ShadowMap(Dictionary<string, List<SyntaxNode>> declarations) =>
        _declarations = declarations;

    public static ShadowMap From(SyntaxNode root)
    {
        var declarations = new Dictionary<string, List<SyntaxNode>>(StringComparer.Ordinal);
        foreach (var node in root.DescendantNodes())
            AddDeclaration(node, declarations);
        return new ShadowMap(declarations);
    }

    /// <summary>
    /// True when the identifier read is lexically bound to a local
    /// variable or parameter declared in an enclosing scope.
    /// </summary>
    public bool IsShadowed(IdentifierNameSyntax identifier)
    {
        if (!_declarations.TryGetValue(identifier.Identifier.Text, out var scopes))
            return false;
        return scopes.Any(scope => scope.Span.Contains(identifier.Span));
    }

    private static void AddDeclaration(SyntaxNode node, Dictionary<string, List<SyntaxNode>> declarations)
    {
        var (name, scope) = ScopeOf(node);
        if (scope is null) return;

        if (!declarations.TryGetValue(name, out var scopes))
        {
            scopes = new List<SyntaxNode>();
            declarations[name] = scopes;
        }
        scopes.Add(scope);
    }

    private static (string Name, SyntaxNode? Scope) ScopeOf(SyntaxNode node)
    {
        return node switch
        {
            ParameterSyntax parameter => (parameter.Identifier.Text, MethodLikeScope(parameter)),
            VariableDeclaratorSyntax declarator => (declarator.Identifier.Text, BlockScope(declarator)),
            SingleVariableDesignationSyntax designation => (designation.Identifier.Text, BlockScope(designation)),
            ForEachStatementSyntax forEach => (forEach.Identifier.Text, BlockScope(forEach)),
            CatchDeclarationSyntax catchDecl => (catchDecl.Identifier.Text, BlockScope(catchDecl)),
            _ => ("", null),
        };
    }

    private static SyntaxNode? MethodLikeScope(SyntaxNode node) =>
        node.Ancestors().FirstOrDefault(IsMethodLike);

    private static bool IsMethodLike(SyntaxNode node) =>
        node is MemberDeclarationSyntax or LocalFunctionStatementSyntax
            or AnonymousFunctionExpressionSyntax;

    private static SyntaxNode? BlockScope(SyntaxNode node) =>
        node.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
}
