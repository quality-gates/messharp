using MessSharp.Rule;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Explicitness ruleset rule factories, keyed by rule class name. These rules
/// have no phpmd counterpart, so they use the MessSharp namespace.
/// </summary>
public static class ExplicitnessRules
{
    public static IReadOnlyDictionary<string, Func<BaseRule>> Factories { get; } =
        new Dictionary<string, Func<BaseRule>>(StringComparer.Ordinal)
        {
            ["MessSharp\\Rule\\Explicitness\\ImplicitInput"] = () => new ImplicitInputRule(),
            ["MessSharp\\Rule\\Explicitness\\ImplicitOutput"] = () => new ImplicitOutputRule(),
            ["MessSharp\\Rule\\Explicitness\\ImplicitInstanceInput"] = () => new ImplicitInstanceInputRule(),
            ["MessSharp\\Rule\\Explicitness\\ImplicitInstanceOutput"] = () => new ImplicitInstanceOutputRule(),
        };

    public static IReadOnlyList<IRule> All =>
        Factories.Values.Select(f => (IRule)f()).ToList();
}
