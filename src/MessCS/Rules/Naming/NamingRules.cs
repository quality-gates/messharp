using MessCS.Rule;

namespace MessCS.Rules.Naming;

/// <summary>
/// Naming ruleset rule factories, keyed by phpmd rule class name.
/// Only edit this file from within the Naming folder's owning task.
/// </summary>
public static class NamingRules
{
    public static IReadOnlyDictionary<string, Func<BaseRule>> Factories { get; } =
        new Dictionary<string, Func<BaseRule>>(StringComparer.Ordinal)
        {
        };

    public static IReadOnlyList<IRule> All =>
        Factories.Values.Select(f => (IRule)f()).ToList();
}
