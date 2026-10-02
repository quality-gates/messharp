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
        SingleVariableDesignationSyntax d => new(d.Identifier.Text, [ExpressionVariableScope(d)]),
        ForEachStatementSyntax f => new(f.Identifier.Text, [f]),
        CatchDeclarationSyntax c => new(c.Identifier.Text, [c.Parent!]),
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
            LocalDeclarationStatementSyntax statement => [statement.Ancestors().First(IsStatementScope)],
            _ => [],
        };

    private static bool IsStatementScope(SyntaxNode node) =>
        node is BlockSyntax or SwitchStatementSyntax or CompilationUnitSyntax;

    /// <summary>
    /// Pattern, out and deconstruction variables are scoped to the nearest
    /// enclosing block, switch section or arm, catch clause, loop or using
    /// header, function body or member.
    /// </summary>
    private static SyntaxNode ExpressionVariableScope(SyntaxNode designation) =>
        designation.Ancestors().First(IsExpressionVariableScope);

    private static bool IsExpressionVariableScope(SyntaxNode node) =>
        IsStatementScope(node) || IsClauseScope(node) || IsFunctionScope(node);

    private static bool IsClauseScope(SyntaxNode node) =>
        node is SwitchSectionSyntax or SwitchExpressionArmSyntax or CatchClauseSyntax
            or ForStatementSyntax or CommonForEachStatementSyntax or UsingStatementSyntax;

    /// <summary>
    /// Top-level statements share one scope, the compilation unit, so a
    /// global statement does not bound its variables.
    /// </summary>
    private static bool IsFunctionScope(SyntaxNode node) =>
        node is AnonymousFunctionExpressionSyntax or ArrowExpressionClauseSyntax
            or (MemberDeclarationSyntax and not GlobalStatementSyntax);
}
