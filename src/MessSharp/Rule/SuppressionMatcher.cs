using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rule;

/// <summary>
/// Checks whether a syntax node declares a suppression via BCL [SuppressMessage]
/// attributes or phpmd-style @SuppressWarnings comments.
/// </summary>
internal static class SuppressionMatcher
{
    public static bool IsNodeSuppressed(SyntaxNode node, IRule rule) =>
        MatchesAttributes(node, rule) || MatchesComments(node, rule);

    private static bool MatchesAttributes(SyntaxNode node, IRule rule)
    {
        foreach (var list in GetAttributeLists(node))
        {
            foreach (var attr in list.Attributes)
            {
                if (MatchesAttribute(attr, rule))
                    return true;
            }
        }
        return false;
    }

    private static IEnumerable<AttributeListSyntax> GetAttributeLists(SyntaxNode node) => node switch
    {
        MemberDeclarationSyntax m => m.AttributeLists,
        CompilationUnitSyntax cu => cu.AttributeLists,
        _ => Enumerable.Empty<AttributeListSyntax>(),
    };

    private static bool MatchesAttribute(AttributeSyntax attr, IRule rule)
    {
        var name = attr.Name.ToString();
        if (!IsSuppressMessageAttribute(name)) return false;

        var args = attr.ArgumentList?.Arguments;
        if (args == null || args.Value.Count == 0) return true;

        string? category = GetStringLiteral(args.Value[0]);
        string? checkId = args.Value.Count > 1 ? GetStringLiteral(args.Value[1]) : null;

        return RuleMatches(rule, category, checkId);
    }

    private static bool IsSuppressMessageAttribute(string name) =>
        name.EndsWith("SuppressMessage", StringComparison.Ordinal) ||
        name.EndsWith("SuppressMessageAttribute", StringComparison.Ordinal);

    private static string? GetStringLiteral(AttributeArgumentSyntax arg)
    {
        if (arg.Expression is LiteralExpressionSyntax lit)
            return lit.Token.ValueText;
        return null;
    }

    private static bool RuleMatches(IRule rule, string? category, string? checkId)
    {
        if (checkId != null)
        {
            if (checkId == "*") return true;
            if (MatchesRuleName(rule, checkId)) return true;
        }

        if (category != null)
        {
            if (category == "*") return true;
            if (checkId == null && MatchesRuleName(rule, category)) return true;
        }

        return false;
    }

    private static bool MatchesRuleName(IRule rule, string text)
    {
        if (string.Equals(text, rule.Name, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(text, rule.Name + "Rule", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(text, rule.SetName, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(text, "PHPMD." + rule.Name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool MatchesComments(SyntaxNode node, IRule rule)
    {
        foreach (var trivia in node.GetLeadingTrivia())
        {
            var text = trivia.ToString();
            if (text.Contains("@SuppressWarnings", StringComparison.OrdinalIgnoreCase)
                && MatchesSuppressWarningsComment(text, rule))
            {
                return true;
            }
        }
        return false;
    }

    private static bool MatchesSuppressWarningsComment(string comment, IRule rule)
    {
        if (comment.Contains("(PHPMD)", StringComparison.OrdinalIgnoreCase)
            || comment.Contains("(\"PHPMD\")", StringComparison.OrdinalIgnoreCase)
            || comment.Contains("(MessSharp)", StringComparison.OrdinalIgnoreCase)
            || comment.Contains("(\"MessSharp\")", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return comment.Contains(rule.Name, StringComparison.OrdinalIgnoreCase)
            || comment.Contains(rule.SetName, StringComparison.OrdinalIgnoreCase);
    }
}
