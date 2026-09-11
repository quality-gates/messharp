using MessSharp.Rule;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.RuleSet;

/// <summary>
/// Resolves and expands &lt;rule ref="..."/&gt; elements across built-in and external ruleset files.
/// </summary>
internal sealed class RuleRefResolver
{
    private readonly Func<string, byte[]> _readRuleset;
    private readonly Func<string, XmlRule, XmlRule, IRule?> _buildRule;
    private readonly Action<RuleSetType, IRule?> _appendRule;
    private readonly IReadOnlyDictionary<string, string> _builtins;

    public RuleRefResolver(
        Func<string, byte[]> readRuleset,
        Func<string, XmlRule, XmlRule, IRule?> buildRule,
        Action<RuleSetType, IRule?> appendRule,
        IReadOnlyDictionary<string, string> builtins)
    {
        _readRuleset = readRuleset;
        _buildRule = buildRule;
        _appendRule = appendRule;
        _builtins = builtins;
    }

    public bool AddRef(RuleSetType set, XmlRule xr, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var (baseName, ruleName) = LoaderFilters.SplitRef(xr.Ref!, _builtins);
        if (!visited.Add(baseName)) return false;

        byte[] data;
        try { data = _readRuleset(baseName); }
        catch (Exception ex)
        {
            throw new FileNotFoundException($"Cannot resolve ref: {xr.Ref}", ex);
        }

        var src = XmlRulesetParser.Deserialize(data);
        var excluded = XmlRuleHelpers.ExcludeSet(xr.Exclude);
        var matched = false;
        foreach (var sr in src.Rules ?? new List<XmlRule>())
        {
            if (ProcessRefRule(set, src.Name ?? "", sr, ruleName, excluded, xr, visited))
                matched = true;
        }

        if (ruleName.Length > 0 && !matched)
            throw new InvalidOperationException($"Cannot resolve rule: {xr.Ref}");

        return matched;
    }

    private bool ProcessRefRule(RuleSetType set, string srcName, XmlRule sr,
        string ruleName, HashSet<string> excluded, XmlRule overrideXr, HashSet<string> visited)
    {
        if (!string.IsNullOrEmpty(sr.Ref))
            return ProcessNestedRef(set, sr, ruleName, excluded, overrideXr, visited);

        if (!string.IsNullOrEmpty(sr.Class))
            return ProcessClassRule(set, srcName, sr, ruleName, excluded, overrideXr);

        return false;
    }

    private bool ProcessNestedRef(RuleSetType set, XmlRule sr, string ruleName,
        HashSet<string> excluded, XmlRule overrideXr, HashSet<string> visited)
    {
        if (ruleName.Length > 0)
            return ResolveNestedRule(set, sr, ruleName, excluded, overrideXr, visited);

        if (!string.IsNullOrEmpty(sr.Name) && excluded.Contains(sr.Name))
            return false;

        AddRef(set, XmlRuleHelpers.MergeChildRef(sr, overrideXr), new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase));
        return true;
    }

    private bool ResolveNestedRule(RuleSetType set, XmlRule sr, string ruleName,
        HashSet<string> excluded, XmlRule overrideXr, HashSet<string> visited)
    {
        var (_, targetRule) = LoaderFilters.SplitRef(sr.Ref!, _builtins);
        if (targetRule == ruleName || sr.Name == ruleName)
            return AddRef(set, XmlRuleHelpers.MergeChildRef(sr, overrideXr), new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(targetRule) && !excluded.Contains(ruleName))
        {
            var child = XmlRuleHelpers.MergeChildRef(sr, overrideXr);
            child.Ref = $"{sr.Ref}/{ruleName}";
            try
            {
                return AddRef(set, child, new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase));
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        return false;
    }

    private bool ProcessClassRule(RuleSetType set, string srcName, XmlRule sr,
        string ruleName, HashSet<string> excluded, XmlRule overrideXr)
    {
        if (ruleName.Length > 0)
        {
            if (sr.Name != ruleName) return false;
            _appendRule(set, _buildRule(srcName, sr, overrideXr));
            return true;
        }

        if (excluded.Contains(sr.Name ?? "")) return false;
        _appendRule(set, _buildRule(srcName, sr, sr));
        return true;
    }
}
