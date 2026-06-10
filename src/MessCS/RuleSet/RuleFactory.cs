using MessCS.Rule;
using MessCS.Rules.CodeSize;

namespace MessCS.RuleSet;

/// <summary>
/// Maps phpmd rule class names to BaseRule factories.
/// Other agents register rules for their group; only this file should be
/// edited to add new mappings.
/// </summary>
public static class RuleFactory
{
    private static readonly Dictionary<string, Func<BaseRule>> _registry =
        new(StringComparer.Ordinal)
        {
            // codesize
            ["PHPMD\\Rule\\CyclomaticComplexity"] = () => new CyclomaticComplexityRule(),
        };

    public static BaseRule? Create(string className)
    {
        _registry.TryGetValue(className, out var factory);
        return factory?.Invoke();
    }

    public static bool IsRegistered(string className) => _registry.ContainsKey(className);
}
