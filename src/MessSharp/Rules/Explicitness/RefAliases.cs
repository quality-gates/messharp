using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The <c>ref</c> locals of one body that alias another local or parameter for their whole life: declared once as
/// <c>ref T r = ref x</c>, never pointed elsewhere, and naming storage declared once in the body. Reading or writing
/// such an alias reads or writes the local it names. Any other <c>ref</c> local stays an unresolved location.
/// </summary>
internal sealed class RefAliases
{
    private readonly Dictionary<string, string> _targets = new(StringComparer.Ordinal);

    public RefAliases(SyntaxNode body)
    {
        var nodes = BodyWalk.Nodes(body).ToList();
        var declared = nodes.Select(Declared).OfType<string>().GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() == 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var repointed = nodes.OfType<AssignmentExpressionSyntax>().Where(assignment => assignment.Right is RefExpressionSyntax)
            .Select(assignment => assignment.Left.ToString()).ToHashSet(StringComparer.Ordinal);
        foreach (var alias in nodes.OfType<VariableDeclaratorSyntax>().Where(IsRefLocal))
            if (Target(alias) is { } target && declared.Contains(alias.Identifier.Text) && declared.Contains(target)
                && !repointed.Contains(alias.Identifier.Text))
                _targets[alias.Identifier.Text] = target;
    }

    /// <summary>The local or parameter <paramref name="name"/> stands for once every alias is followed.</summary>
    public string Canonical(string name)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (_targets.TryGetValue(name, out var target) && seen.Add(name)) name = target;
        return name;
    }

    /// <summary>A binding written through an alias rebinds the local the alias names.</summary>
    public StorageBinding Rebound(StorageBinding binding) =>
        binding.Node is VariableDeclaratorSyntax ? binding : binding with { Name = Canonical(binding.Name) };

    /// <summary>The reference a tracked alias is declared with, or null when <paramref name="declarator"/> is none.</summary>
    public IdentifierNameSyntax? Aliased(VariableDeclaratorSyntax declarator) =>
        _targets.ContainsKey(declarator.Identifier.Text) && IsRefLocal(declarator)
            ? (IdentifierNameSyntax)CallableSyntax.Unwrap(((RefExpressionSyntax)declarator.Initializer!.Value).Expression)
            : null;

    private bool IsAlias(VariableDeclaratorSyntax declarator) => Aliased(declarator) is not null;

    /// <summary>Whether <paramref name="reference"/> is the location a tracked alias is declared with.</summary>
    public bool Initializes(RefExpressionSyntax reference) => reference.Parent is EqualsValueClauseSyntax
    {
        Parent: VariableDeclaratorSyntax declarator,
    } && IsAlias(declarator);

    private static bool IsRefLocal(VariableDeclaratorSyntax declarator) =>
        declarator.Parent is VariableDeclarationSyntax { Type: RefTypeSyntax, Parent: LocalDeclarationStatementSyntax };

    private static string? Target(VariableDeclaratorSyntax alias) =>
        alias.Initializer?.Value is RefExpressionSyntax reference
        && CallableSyntax.Unwrap(reference.Expression) is IdentifierNameSyntax name ? name.Identifier.Text : null;

    private static string? Declared(SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax variable => variable.Identifier.Text,
        SingleVariableDesignationSyntax designation => designation.Identifier.Text,
        ParameterSyntax parameter => parameter.Identifier.Text,
        ForEachStatementSyntax loop => loop.Identifier.Text,
        CatchDeclarationSyntax catcher => catcher.Identifier.Text,
        _ => null,
    };
}
