using MessCS.Rule;
using MessCS.Rules.CleanCode;
using MessCS.Rules.CodeSize;
using MessCS.Rules.Controversial;
using MessCS.Rules.Design;
using MessCS.Rules.Naming;
using MessCS.Rules.UnusedCode;

namespace MessCS.Rules;

/// <summary>
/// Maps ruleset short names to their rule factories.
/// Each group class lives in its own folder and must not be edited by other
/// agents.
/// </summary>
public static class Registry
{
    private static readonly Dictionary<string, Func<IReadOnlyList<IRule>>> _groups =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["cleancode"]     = () => CleanCodeRules.All,
            ["codesize"]      = () => CodeSizeRules.All,
            ["controversial"] = () => ControvRules.All,
            ["design"]        = () => DesignRules.All,
            ["naming"]        = () => NamingRules.All,
            ["unusedcode"]    = () => UnusedCodeRules.All,
        };

    public static IReadOnlyList<IRule> GetRules(string rulesetName)
    {
        if (_groups.TryGetValue(rulesetName, out var factory))
            return factory();
        return Array.Empty<IRule>();
    }

    public static bool IsKnown(string rulesetName) => _groups.ContainsKey(rulesetName);

    public static IEnumerable<string> KnownNames => _groups.Keys;
}
