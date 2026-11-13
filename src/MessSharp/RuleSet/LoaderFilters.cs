using MessSharp.Rule;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.RuleSet;

/// <summary>
/// Static utilities for filtering and deduplicating loaded rule sets and splitting rule refs.
/// </summary>
internal static class LoaderFilters
{
    /// <summary>
    /// Keeps the last definition of each rule name. A definition dropped in favour of one
    /// from a different top-level ruleset is reported through <paramref name="warn"/>,
    /// because the two may be independent rules rather than an intentional override.
    /// </summary>
    internal static void DedupeRules(List<RuleSetType> sets, Action<string>? warn = null)
    {
        var winners = new Dictionary<string, int>(StringComparer.Ordinal);
        var winnerRules = new Dictionary<string, IRule>(StringComparer.Ordinal);
        for (int i = sets.Count - 1; i >= 0; i--)
        {
            var set = sets[i];
            var kept = new List<IRule>();
            for (int j = set.Rules.Count - 1; j >= 0; j--)
            {
                var r = set.Rules[j];
                if (winners.TryAdd(r.Name, i))
                {
                    winnerRules[r.Name] = r;
                    kept.Add(r);
                }
                else if (winners[r.Name] != i && !IsSameDefinition(r, winnerRules[r.Name]))
                    warn?.Invoke(CollisionMessage(r.Name, Label(sets, i), Label(sets, winners[r.Name])));
            }
            kept.Reverse();
            set.Rules.Clear();
            set.Rules.AddRange(kept);
        }
    }

    // Composite rulesets re-export the same bundled rule; identical definitions are not a conflict.
    private static bool IsSameDefinition(IRule a, IRule b) =>
        a is BaseRule x && b is BaseRule y
        && x.GetType() == y.GetType()
        && x.Priority == y.Priority
        && x.Message == y.Message
        && x.RuleProps.HasSameValuesAs(y.RuleProps);

    private static string CollisionMessage(string rule, string dropped, string kept) =>
        $"Rule '{rule}' from {dropped} is replaced by the later definition in {kept}; only the last definition runs";

    private static string Label(List<RuleSetType> sets, int index) =>
        string.IsNullOrEmpty(sets[index].Name) ? $"ruleset #{index + 1}" : $"ruleset '{sets[index].Name}'";

    internal static void FilterRules(List<RuleSetType> sets,
        IReadOnlyList<string> enable, IReadOnlyList<string> disable)
    {
        if (enable.Count == 0 && disable.Count == 0) return;
        var enabled = ToSet(enable);
        var disabled = ToSet(disable);
        foreach (var set in sets)
        {
            var kept = new List<IRule>();
            foreach (var r in set.Rules)
            {
                if (enabled.Count > 0 && !enabled.Contains(r.Name)) continue;
                if (disabled.Contains(r.Name)) continue;
                kept.Add(r);
            }
            set.Rules.Clear();
            set.Rules.AddRange(kept);
        }
    }

    private static HashSet<string> ToSet(IReadOnlyList<string> names) =>
        new(names, StringComparer.Ordinal);

    internal static (string baseName, string ruleName) SplitRef(
        string refStr, IReadOnlyDictionary<string, string> builtins)
    {
        if (IsResolvable(refStr, builtins)) return (refStr, "");
        int idx = refStr.LastIndexOf('/');
        if (idx >= 0 && IsResolvable(refStr[..idx], builtins))
            return (refStr[..idx], refStr[(idx + 1)..]);
        if (BuiltInRuleIndex.TryGetRuleset(refStr, out var ruleset))
            return (ruleset, refStr);
        return (refStr, "");
    }

    internal static bool IsResolvable(
        string ident, IReadOnlyDictionary<string, string> builtins) =>
        builtins.ContainsKey(ident) || File.Exists(ident);
}
