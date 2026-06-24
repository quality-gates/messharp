using MessSharp.Rule;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.RuleSet;

/// <summary>
/// Loads phpmd-format ruleset XML files into RuleSets.
/// Mirrors messgo's ruleset.Loader.
/// </summary>
public sealed class Loader
{
    /// <summary>Drop rules with priority > MinPriority (less important). 0 = no limit.</summary>
    public int MinPriority { get; set; }

    /// <summary>Drop rules with priority &lt; MaxPriority (more important). 0 = no limit.</summary>
    public int MaxPriority { get; set; } = 1;

    /// <summary>Receives diagnostic messages about skipped/unknown rules.</summary>
    public Action<string>? Warn { get; set; }

    // Known ruleset short names -> XML file names relative to rulesets/ dir.
    private static readonly Dictionary<string, string> BuiltinNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["cleancode"] = "cleancode.xml",
            ["codesize"] = "codesize.xml",
            ["controversial"] = "controversial.xml",
            ["design"] = "design.xml",
            ["naming"] = "naming.xml",
            ["unusedcode"] = "unusedcode.xml",
            ["csharp"] = "csharp.xml",
            ["opinionated"] = "opinionated.xml",
        };

    public static IReadOnlyList<string> BuiltinRulesetNames =>
        new[] { "cleancode", "codesize", "controversial", "design", "naming", "unusedcode", "csharp", "opinionated" };

    /// <summary>Resolves a comma-separated spec of ruleset names or file paths.</summary>
    public List<RuleSetType> Load(string spec)
    {
        var sets = new List<RuleSetType>();
        foreach (var part in spec.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0))
            sets.Add(Parse(ReadRuleset(part)));
        LoaderFilters.DedupeRules(sets);
        return sets;
    }

    private byte[] ReadRuleset(string ident)
    {
        var path = ResolvePath(ident);
        if (path == null) throw new FileNotFoundException($"Unknown ruleset or file: {ident}");
        return File.ReadAllBytes(path);
    }

    private string? ResolvePath(string ident)
    {
        if (BuiltinNames.TryGetValue(ident, out var filename))
        {
            var resolved = LoaderFileResolver.FindBuiltinFile(filename);
            if (resolved != null) return resolved;
        }
        return File.Exists(ident) ? ident : null;
    }

    private RuleSetType Parse(byte[] data)
    {
        var xrs = XmlRulesetParser.Deserialize(data);
        var set = new RuleSetType { Name = xrs.Name ?? "", Description = xrs.Description?.Trim() ?? "" };
        foreach (var xr in xrs.Rules ?? new List<XmlRule>())
            AddRule(set, xrs.Name ?? "", xr);
        return set;
    }

    private void AddRule(RuleSetType set, string setName, XmlRule xr)
    {
        if (!string.IsNullOrEmpty(xr.Ref)) { AddRef(set, xr); return; }
        if (!string.IsNullOrEmpty(xr.Class)) { AppendIfNotNull(set, BuildRule(setName, xr, xr)); }
    }

    private void AddRef(RuleSetType set, XmlRule xr)
    {
        var (baseName, ruleName) = SplitRef(xr.Ref!);
        byte[] data;
        try { data = ReadRuleset(baseName); }
        catch { WarnMsg($"Cannot resolve ref: {xr.Ref}"); return; }

        var src = XmlRulesetParser.Deserialize(data);
        var excluded = XmlRuleHelpers.ExcludeSet(xr.Exclude);
        foreach (var sr in src.Rules ?? new List<XmlRule>())
            ProcessRefRule(set, src.Name ?? "", sr, ruleName, excluded, xr);
    }

    private void ProcessRefRule(RuleSetType set, string srcName, XmlRule sr,
        string ruleName, HashSet<string> excluded, XmlRule overrideXr)
    {
        if (string.IsNullOrEmpty(sr.Class)) return;
        if (ruleName.Length > 0)
        {
            if (sr.Name == ruleName) AppendIfNotNull(set, BuildRule(srcName, sr, overrideXr));
            return;
        }
        if (!excluded.Contains(sr.Name ?? ""))
            AppendIfNotNull(set, BuildRule(srcName, sr, sr));
    }

    private IRule? BuildRule(string setName, XmlRule def, XmlRule ov)
    {
        if (string.IsNullOrEmpty(def.Class)) return null;
        var rule = RuleFactory.Create(def.Class!);
        if (rule == null) { WarnMsg($"Skipping unimplemented rule {def.Name} ({def.Class})"); return null; }
        XmlRuleHelpers.PopulateRule(rule, setName, def, ov);
        return rule;
    }

    private void AppendIfNotNull(RuleSetType set, IRule? rule)
    {
        if (rule is not BaseRule br) return;
        if (MinPriority > 0 && br.Priority > MinPriority) return;
        if (MaxPriority > 0 && br.Priority < MaxPriority) return;
        set.Rules.Add(rule);
    }

    private (string baseName, string ruleName) SplitRef(string refStr)
    {
        if (IsResolvable(refStr)) return (refStr, "");
        int idx = refStr.LastIndexOf('/');
        if (idx >= 0 && IsResolvable(refStr[..idx]))
            return (refStr[..idx], refStr[(idx + 1)..]);
        return (refStr, "");
    }

    private bool IsResolvable(string ident) =>
        BuiltinNames.ContainsKey(ident) || File.Exists(ident);

    /// <summary>Narrows rule sets in place by enable/disable name lists.</summary>
    public static void FilterRules(List<RuleSetType> sets,
        IReadOnlyList<string> enable, IReadOnlyList<string> disable) =>
        LoaderFilters.FilterRules(sets, enable, disable);

    private void WarnMsg(string msg) => Warn?.Invoke(msg);
}
