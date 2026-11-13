using MessSharp.Rule;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.RuleSet;

/// <summary>
/// Resolves and expands &lt;rule ref="..."/&gt; elements across built-in and external ruleset files.
/// </summary>
internal sealed class RuleRefResolver
{
    private readonly Loader _loader;

    public RuleRefResolver(Loader loader) => _loader = loader;

    public bool AddRef(RuleSetType set, XmlRule xr, RefTrail? trail = null)
    {
        var (baseName, ruleName) = LoaderFilters.SplitRef(xr.Ref!, Loader.BuiltinNames);
        var inner = Descend(trail, baseName, xr.Ref!);
        var data = ReadOrFail(baseName, xr.Ref!);

        var src = XmlRulesetParser.Deserialize(data);
        var excluded = XmlRuleHelpers.ExcludeSet(xr.Exclude);
        var matched = false;
        foreach (var sr in src.Rules ?? new List<XmlRule>())
        {
            if (ProcessRefRule(set, src.Name ?? "", sr, ruleName, excluded, xr, inner))
                matched = true;
        }

        if (ruleName.Length > 0 && !matched)
            throw new InvalidOperationException($"Cannot resolve rule: {xr.Ref}");

        return matched;
    }

    // A ref back to an ancestor ruleset is a cycle: it can never resolve,
    // so fail like any other unresolved ref instead of loading nothing.
    // InvalidDataException is deliberately not an InvalidOperationException,
    // which ResolveNestedRule swallows while probing nested sets.
    private static RefTrail Descend(RefTrail? trail, string baseName, string reference) =>
        (trail ?? RefTrail.Start).Enter(baseName)
        ?? throw new InvalidDataException($"Cyclic ruleset ref: {reference}");

    private byte[] ReadOrFail(string baseName, string reference)
    {
        try { return _loader.ReadRuleset(baseName); }
        catch (Exception ex)
        {
            throw new FileNotFoundException($"Cannot resolve ref: {reference}", ex);
        }
    }

    private bool ProcessRefRule(RuleSetType set, string srcName, XmlRule sr,
        string ruleName, HashSet<string> excluded, XmlRule overrideXr, RefTrail trail)
    {
        if (!string.IsNullOrEmpty(sr.Ref))
            return ProcessNestedRef(set, sr, ruleName, excluded, overrideXr, trail);

        if (!string.IsNullOrEmpty(sr.Class))
            return ProcessClassRule(set, srcName, sr, ruleName, excluded, overrideXr);

        return false;
    }

    private bool ProcessNestedRef(RuleSetType set, XmlRule sr, string ruleName,
        HashSet<string> excluded, XmlRule overrideXr, RefTrail trail)
    {
        if (ruleName.Length > 0)
            return ResolveNestedRule(set, sr, ruleName, excluded, overrideXr, trail);

        if (!string.IsNullOrEmpty(sr.Name) && excluded.Contains(sr.Name))
            return false;

        AddRef(set, XmlRuleHelpers.MergeChildRef(sr, overrideXr), trail);
        return true;
    }

    private bool ResolveNestedRule(RuleSetType set, XmlRule sr, string ruleName,
        HashSet<string> excluded, XmlRule overrideXr, RefTrail trail)
    {
        var (_, targetRule) = LoaderFilters.SplitRef(sr.Ref!, Loader.BuiltinNames);
        if (targetRule == ruleName || sr.Name == ruleName)
            return AddRef(set, XmlRuleHelpers.MergeChildRef(sr, overrideXr), trail);

        if (string.IsNullOrEmpty(targetRule) && !excluded.Contains(ruleName))
        {
            var child = XmlRuleHelpers.MergeChildRef(sr, overrideXr);
            child.Ref = $"{sr.Ref}/{ruleName}";
            try
            {
                return AddRef(set, child, trail);
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
            _loader.AppendIfNotNull(set, _loader.BuildRule(srcName, sr, overrideXr));
            return true;
        }

        if (excluded.Contains(sr.Name ?? "")) return false;
        _loader.AppendIfNotNull(set, _loader.BuildRule(srcName, sr, sr));
        return true;
    }
}
