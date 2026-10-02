using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules;

/// <summary>
/// A parameter or local variable and the nodes whose span its name is
/// visible in, following C#'s lexical scoping rules on syntax alone.
/// </summary>
internal readonly record struct LocalDeclaration(string Name, IReadOnlyList<SyntaxNode> Scopes)
{
    /// <summary>
    /// The declaration <paramref name="node"/> introduces, or null when it
    /// declares no parameter or local (fields and events are members).
    /// </summary>
    public static LocalDeclaration? For(SyntaxNode node) => node switch
    {
        ParameterSyntax p => new(p.Identifier.Text, ParameterScopes(p)),
        VariableDeclaratorSyntax v => new(v.Identifier.Text, DeclaratorScopes(v)),
        SingleVariableDesignationSyntax d => new(d.Identifier.Text, EnclosingScopes(d, IsExpressionVariableScope)),
        ForEachStatementSyntax f => new(f.Identifier.Text, [f]),
        CatchDeclarationSyntax { Parent: CatchClauseSyntax clause } c => new(c.Identifier.Text, [clause]),
        _ => null,
    };

    /// <summary>
    /// Parameters are visible in the method, local function, lambda,
    /// anonymous method or indexer that declares them.
    /// </summary>
    private static IReadOnlyList<SyntaxNode> ParameterScopes(ParameterSyntax parameter)
    {
        var owner = parameter.Parent is BaseParameterListSyntax list ? list.Parent : parameter.Parent;
        if (owner is TypeDeclarationSyntax type) return PrimaryConstructorScopes(type);
        return owner is null ? [] : [owner];
    }

    /// <summary>
    /// A primary constructor parameter binds only in the base list and in
    /// member initializers; in member bodies a same-named member wins.
    /// </summary>
    private static IReadOnlyList<SyntaxNode> PrimaryConstructorScopes(TypeDeclarationSyntax type)
    {
        var scopes = new List<SyntaxNode>();
        if (type.BaseList != null) scopes.Add(type.BaseList);
        scopes.AddRange(type.Members.SelectMany(MemberInitializers));
        return scopes;
    }

    private static IEnumerable<SyntaxNode> MemberInitializers(MemberDeclarationSyntax member) => member switch
    {
        BaseFieldDeclarationSyntax field => field.Declaration.Variables
            .Select(v => v.Initializer).OfType<SyntaxNode>(),
        PropertyDeclarationSyntax { Initializer: { } initializer } => [initializer],
        _ => [],
    };

    /// <summary>
    /// A declarator in a for, using or fixed header is scoped to that
    /// statement; a local declaration statement to its enclosing block.
    /// </summary>
    private static IReadOnlyList<SyntaxNode> DeclaratorScopes(VariableDeclaratorSyntax declarator) =>
        declarator.Parent?.Parent switch
        {
            ForStatementSyntax or UsingStatementSyntax or FixedStatementSyntax => [declarator.Parent.Parent],
            LocalDeclarationStatementSyntax statement => EnclosingScopes(statement, IsStatementScope),
            _ => [],
        };

    /// <summary>
    /// The nearest enclosing scope of <paramref name="node"/>. Top-level
    /// statements share one scope: all the global statements of the file,
    /// but not the type declarations between them.
    /// </summary>
    private static IReadOnlyList<SyntaxNode> EnclosingScopes(SyntaxNode node, Func<SyntaxNode, bool> isScope) =>
        node.Ancestors().FirstOrDefault(isScope) switch
        {
            null => [],
            GlobalStatementSyntax { Parent: CompilationUnitSyntax unit } => unit.Members.OfType<GlobalStatementSyntax>().ToList(),
            var scope => [scope],
        };

    private static bool IsStatementScope(SyntaxNode node) =>
        node is BlockSyntax or SwitchStatementSyntax or GlobalStatementSyntax;

    /// <summary>
    /// Pattern, out and deconstruction variables are scoped to the nearest
    /// enclosing block, switch section or arm, catch clause, loop or using
    /// header, base list, function body or member.
    /// </summary>
    private static bool IsExpressionVariableScope(SyntaxNode node) =>
        IsStatementScope(node) || IsClauseScope(node) || IsFunctionScope(node);

    private static bool IsClauseScope(SyntaxNode node) =>
        node is SwitchSectionSyntax or SwitchExpressionArmSyntax or CatchClauseSyntax
            or ForStatementSyntax or CommonForEachStatementSyntax or UsingStatementSyntax or BaseListSyntax;

    private static bool IsFunctionScope(SyntaxNode node) =>
        node is AnonymousFunctionExpressionSyntax or ArrowExpressionClauseSyntax or MemberDeclarationSyntax;
}
