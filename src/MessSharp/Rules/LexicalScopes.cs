using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules;

/// <summary>
/// The one syntax-only answer to "does this name refer to an enclosing local
/// variable, parameter or range variable rather than a member of the containing
/// type?". Every rule that must not treat a shadowed name as member or type use
/// asks here, so scoping fixes apply to the design, explicitness and unused-code
/// rules alike.
/// </summary>
internal static class LexicalScopes
{
    /// <summary>True when <paramref name="name"/>, as used at <paramref name="usage"/>, is bound by a declaration in an enclosing scope of the same member.</summary>
    internal static bool IsShadowed(SyntaxNode usage, string name)
    {
        for (var scope = usage.Parent; scope != null; scope = scope.Parent)
        {
            if (scope is QueryExpressionSyntax query && QueryRangeScope.IsShadowed(query, usage, name)) return true;
            if (ScopeDeclarations.Declares(scope, name)) return true;
            if (scope is MemberDeclarationSyntax) break;
        }
        return false;
    }

    /// <summary>True when the identifier read is bound by a local, parameter or range variable.</summary>
    internal static bool IsShadowed(IdentifierNameSyntax identifier) => IsShadowed(identifier, identifier.Identifier.Text);
}
