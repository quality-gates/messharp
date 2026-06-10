using MessCS.Rule;

namespace MessCS.Rules.CodeSize;

/// <summary>
/// CodeSize ruleset rule factories, keyed by phpmd rule class name.
/// Only edit this file from within the CodeSize folder's owning task.
/// </summary>
public static class CodeSizeRules
{
    public static IReadOnlyDictionary<string, Func<BaseRule>> Factories { get; } =
        new Dictionary<string, Func<BaseRule>>(StringComparer.Ordinal)
        {
            ["PHPMD\\Rule\\CyclomaticComplexity"] = () => new CyclomaticComplexityRule(),
        };

    public static IReadOnlyList<IRule> All =>
        Factories.Values.Select(f => (IRule)f()).ToList();
}
