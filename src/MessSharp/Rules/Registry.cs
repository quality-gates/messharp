using MessSharp.Rule;
using MessSharp.Rules.CleanCode;
using MessSharp.Rules.CodeSize;
using MessSharp.Rules.Controversial;
using MessSharp.Rules.Design;
using MessSharp.Rules.Naming;
using MessSharp.Rules.UnusedCode;

namespace MessSharp.Rules;

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
