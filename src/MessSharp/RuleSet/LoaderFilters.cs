using MessSharp.Rule;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.RuleSet;

/// <summary>
/// Static utilities for filtering and deduplicating loaded rule sets.
/// Extracted from Loader to reduce that class's weighted method count.
/// </summary>
internal static class LoaderFilters
{
    internal static void DedupeRules(List<RuleSetType> sets)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var set in sets)
        {
            var kept = new List<IRule>();
            foreach (var r in set.Rules)
            {
                if (seen.Add(r.Name))
                    kept.Add(r);
            }
            set.Rules.Clear();
            set.Rules.AddRange(kept);
        }
    }

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
        return (refStr, "");
    }

    internal static bool IsResolvable(
        string ident, IReadOnlyDictionary<string, string> builtins) =>
        builtins.ContainsKey(ident) || File.Exists(ident);
}
