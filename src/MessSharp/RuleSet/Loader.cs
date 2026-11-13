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
    internal static readonly Dictionary<string, string> BuiltinNames =
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
            ["explicitness"] = "explicitness.xml",
            ["explicitness-strict"] = "explicitness-strict.xml",
        };

    public static IReadOnlyList<string> BuiltinRulesetNames =>
        new[] { "cleancode", "codesize", "controversial", "design", "naming", "unusedcode", "csharp", "opinionated",
            "explicitness", "explicitness-strict" };

    /// <summary>
    /// Resolves a comma-separated spec of ruleset names or file paths, optionally
    /// narrowed by enable/disable rule-name lists (null or empty means no narrowing).
    /// </summary>
    public List<RuleSetType> Load(string spec,
        IReadOnlyList<string>? enable = null, IReadOnlyList<string>? disable = null)
    {
        var sets = new List<RuleSetType>();
        foreach (var part in spec.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0))
            sets.Add(Parse(ReadRuleset(part)));
        LoaderFilters.DedupeRules(sets);
        LoaderFilters.FilterRules(sets, enable ?? Array.Empty<string>(), disable ?? Array.Empty<string>());
        return sets;
    }

    internal byte[] ReadRuleset(string ident)
    {
        if (BuiltinNames.TryGetValue(ident, out var filename))
        {
            var builtIn = BuiltInRuleSetReader.Read(filename);
            if (builtIn != null) return builtIn;
        }
        if (File.Exists(ident)) return File.ReadAllBytes(ident);
        throw new FileNotFoundException($"Unknown ruleset or file: {ident}");
    }

    private RuleSetType Parse(byte[] data)
    {
        var xrs = XmlRulesetParser.Deserialize(data);
        var set = new RuleSetType { Name = xrs.Name ?? "", Description = xrs.Description?.Trim() ?? "" };
        foreach (var xr in xrs.Rules ?? new List<XmlRule>())
            AddRule(set, xrs.Name ?? "", xr);
        return set;
    }

    private readonly RuleRefResolver _refResolver;

    public Loader()
    {
        _refResolver = new RuleRefResolver(this);
    }

    private void AddRule(RuleSetType set, string setName, XmlRule xr)
    {
        if (!string.IsNullOrEmpty(xr.Ref)) { _refResolver.AddRef(set, xr); return; }
        if (!string.IsNullOrEmpty(xr.Class)) { AppendIfNotNull(set, BuildRule(setName, xr, xr)); }
    }

    internal IRule? BuildRule(string setName, XmlRule def, XmlRule ov)
    {
        if (string.IsNullOrEmpty(def.Class)) return null;
        var rule = RuleFactory.Create(def.Class!);
        if (rule == null) { WarnMsg($"Skipping unimplemented rule {def.Name} ({def.Class})"); return null; }
        WarnInvalidPriority(def, ov);
        XmlRuleHelpers.PopulateRule(rule, setName, def, ov);
        return rule;
    }

    private void WarnInvalidPriority(XmlRule def, XmlRule ov)
    {
        var raw = XmlRuleHelpers.EffectiveInvalidPriorityText(def, ov);
        if (raw != null)
            WarnMsg($"Invalid priority '{raw}' for rule '{def.Name ?? ov.Name ?? "(unnamed)"}'; using priority 0");
    }

    internal void AppendIfNotNull(RuleSetType set, IRule? rule)
    {
        if (rule is not BaseRule br) return;
        if (MinPriority > 0 && br.Priority > MinPriority) return;
        if (MaxPriority > 0 && br.Priority < MaxPriority) return;
        set.Rules.Add(rule);
    }

    private void WarnMsg(string msg) => Warn?.Invoke(msg);
}
