using System.Xml;
using MessCS.Rule;
using RuleSetType = MessCS.Rule.RuleSet;

namespace MessCS.RuleSet;

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
            ["cleancode"]     = "cleancode.xml",
            ["codesize"]      = "codesize.xml",
            ["controversial"] = "controversial.xml",
            ["design"]        = "design.xml",
            ["naming"]        = "naming.xml",
            ["unusedcode"]    = "unusedcode.xml",
            ["csharp"]        = "csharp.xml",
            ["opinionated"]   = "opinionated.xml",
        };

    public static IReadOnlyList<string> BuiltinRulesetNames =>
        new[] { "cleancode", "codesize", "controversial", "design", "naming", "unusedcode", "csharp", "opinionated" };

    /// <summary>
    /// Resolves a comma-separated spec of ruleset names or file paths.
    /// </summary>
    public List<RuleSetType> Load(string spec)
    {
        var sets = new List<RuleSetType>();
        foreach (var part in spec.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            var data = ReadRuleset(part);
            var set = Parse(data);
            sets.Add(set);
        }
        DedupeRules(sets);
        return sets;
    }

    private byte[] ReadRuleset(string ident)
    {
        var path = ResolvePath(ident);
        if (path == null)
            throw new FileNotFoundException($"Unknown ruleset or file: {ident}");
        return File.ReadAllBytes(path);
    }

    private string? ResolvePath(string ident)
    {
        if (BuiltinNames.TryGetValue(ident, out var filename))
        {
            var resolved = FindBuiltinFile(filename);
            if (resolved != null) return resolved;
        }
        if (File.Exists(ident)) return ident;
        return null;
    }

    private static string? FindBuiltinFile(string filename)
    {
        // 1. Next to the executable (output dir)
        var exeDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(exeDir, "rulesets", filename);
        if (File.Exists(candidate)) return candidate;

        // 2. Repo-relative fallback (for running from source)
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var p = Path.Combine(dir, "rulesets", filename);
            if (File.Exists(p)) return p;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == null) break;
            dir = parent;
        }
        return null;
    }

    private RuleSetType Parse(byte[] data)
    {
        var xrs = DeserializeXml(data);
        var set = new RuleSetType
        {
            Name = xrs.Name ?? "",
            Description = xrs.Description?.Trim() ?? "",
        };
        foreach (var xr in xrs.Rules ?? new List<XmlRule>())
            AddRule(set, xrs.Name ?? "", xr);
        return set;
    }

    private void AddRule(RuleSetType set, string setName, XmlRule xr)
    {
        if (!string.IsNullOrEmpty(xr.Ref))
        {
            AddRef(set, xr);
            return;
        }
        if (!string.IsNullOrEmpty(xr.Class))
        {
            var r = BuildRule(setName, xr, xr);
            if (r != null) AppendRule(set, r);
        }
    }

    private void AddRef(RuleSetType set, XmlRule xr)
    {
        var (baseName, ruleName) = SplitRef(xr.Ref!);
        byte[] data;
        try { data = ReadRuleset(baseName); }
        catch { WarnMsg($"Cannot resolve ref: {xr.Ref}"); return; }

        var src = DeserializeXml(data);
        var excluded = ExcludeSet(xr.Exclude);

        foreach (var sr in src.Rules ?? new List<XmlRule>())
            ProcessRefRule(set, src.Name ?? "", sr, ruleName, excluded, xr);
    }

    private void ProcessRefRule(RuleSetType set, string srcName, XmlRule sr,
        string ruleName, HashSet<string> excluded, XmlRule overrideXr)
    {
        if (string.IsNullOrEmpty(sr.Class)) return;
        if (ruleName.Length > 0)
        {
            if (sr.Name == ruleName)
            {
                var r = BuildRule(srcName, sr, overrideXr);
                if (r != null) AppendRule(set, r);
            }
            return;
        }
        if (excluded.Contains(sr.Name ?? "")) return;
        var rule = BuildRule(srcName, sr, sr);
        if (rule != null) AppendRule(set, rule);
    }

    private IRule? BuildRule(string setName, XmlRule def, XmlRule ov)
    {
        if (string.IsNullOrEmpty(def.Class)) return null;

        var rule = RuleFactory.Create(def.Class!);
        if (rule == null)
        {
            WarnMsg($"Skipping unimplemented rule {def.Name} ({def.Class})");
            return null;
        }

        rule.Name = def.Name ?? "";
        rule.Message = def.Message?.Trim() ?? "";
        rule.SetName = setName;
        rule.ExternalUrl = def.ExternalInfoUrl ?? "";
        rule.Since = def.Since ?? "";
        rule.Description = def.Description?.Trim() ?? "";
        rule.Priority = 3;

        if (def.Priority.HasValue)
            rule.Priority = def.Priority.Value;

        rule.RuleProps = MergeProps(def.Properties, ov.Properties);

        if (ov.Priority.HasValue)
            rule.Priority = ov.Priority.Value;

        return rule;
    }

    private void AppendRule(RuleSetType set, IRule rule)
    {
        if (rule is not BaseRule br) return;
        int prio = br.Priority;
        if (MinPriority > 0 && prio > MinPriority) return;
        if (MaxPriority > 0 && prio < MaxPriority) return;
        set.Rules.Add(rule);
    }

    private (string baseName, string ruleName) SplitRef(string refStr)
    {
        if (IsResolvable(refStr)) return (refStr, "");
        int idx = refStr.LastIndexOf('/');
        if (idx >= 0)
        {
            var left = refStr[..idx];
            if (IsResolvable(left))
                return (left, refStr[(idx + 1)..]);
        }
        return (refStr, "");
    }

    private bool IsResolvable(string ident) =>
        BuiltinNames.ContainsKey(ident) || File.Exists(ident);

    private static HashSet<string> ExcludeSet(List<XmlExclude>? excludes)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (excludes != null)
            foreach (var e in excludes)
                if (e.Name != null) set.Add(e.Name);
        return set;
    }

    private static Properties MergeProps(XmlProperties? baseProps, XmlProperties? overProps)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (baseProps?.Property != null)
            foreach (var p in baseProps.Property) map[p.Name ?? ""] = PropValue(p);
        if (overProps?.Property != null)
            foreach (var p in overProps.Property) map[p.Name ?? ""] = PropValue(p);
        return new Properties(map);
    }

    private static string PropValue(XmlProperty p)
    {
        if (string.IsNullOrEmpty(p.Value) && !string.IsNullOrEmpty(p.InnerValue))
            return p.InnerValue.Trim();
        return p.Value ?? "";
    }

    private static void DedupeRules(List<RuleSetType> sets)
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

    /// <summary>
    /// Narrows rule sets in place by enable/disable name lists.
    /// </summary>
    public static void FilterRules(List<RuleSetType> sets,
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

    private void WarnMsg(string msg) => Warn?.Invoke(msg);

    // -----------------------------------------------------------------------
    // XML deserialization
    // -----------------------------------------------------------------------

    private static XmlRuleSet DeserializeXml(byte[] data)
    {
        using var ms = new MemoryStream(data);
        // Use XmlReader to handle CDATA and namespace variations.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
        using var reader = XmlReader.Create(ms, settings);
        var xrs = new XmlRuleSet();
        ParseXmlRuleSet(reader, xrs);
        return xrs;
    }

    private static void ParseXmlRuleSet(XmlReader r, XmlRuleSet xrs)
    {
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.Element)
            {
                switch (r.LocalName)
                {
                    case "ruleset":
                        xrs.Name = r.GetAttribute("name");
                        break;
                    case "description" when xrs.Rules == null:
                        xrs.Description = r.ReadElementContentAsString();
                        break;
                    case "rule":
                        var rule = ParseRule(r);
                        (xrs.Rules ??= new()).Add(rule);
                        break;
                }
            }
        }
    }

    private static XmlRule ParseRule(XmlReader r)
    {
        var rule = new XmlRule
        {
            Name = r.GetAttribute("name"),
            Message = r.GetAttribute("message"),
            Class = r.GetAttribute("class"),
            Ref = r.GetAttribute("ref"),
            ExternalInfoUrl = r.GetAttribute("externalInfoUrl"),
            Since = r.GetAttribute("since"),
        };

        if (r.IsEmptyElement) return rule;

        int depth = r.Depth;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == depth)
                break;
            if (r.NodeType != XmlNodeType.Element) continue;

            switch (r.LocalName)
            {
                case "description":
                    rule.Description = r.ReadElementContentAsString().Trim();
                    break;
                case "priority":
                    if (int.TryParse(r.ReadElementContentAsString(), out var p))
                        rule.Priority = p;
                    break;
                case "properties":
                    rule.Properties = ParseProperties(r);
                    break;
                case "exclude":
                    (rule.Exclude ??= new()).Add(new XmlExclude { Name = r.GetAttribute("name") });
                    break;
            }
        }
        return rule;
    }

    private static XmlProperties ParseProperties(XmlReader r)
    {
        var props = new XmlProperties { Property = new() };
        if (r.IsEmptyElement) return props;
        int depth = r.Depth;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == depth) break;
            if (r.NodeType != XmlNodeType.Element || r.LocalName != "property") continue;
            var p = new XmlProperty
            {
                Name = r.GetAttribute("name"),
                Value = r.GetAttribute("value"),
            };
            if (!r.IsEmptyElement)
            {
                // Try to read <value> inner element
                int pd = r.Depth;
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.EndElement && r.Depth == pd) break;
                    if (r.NodeType == XmlNodeType.Element && r.LocalName == "value")
                        p.InnerValue = r.ReadElementContentAsString();
                }
            }
            props.Property.Add(p);
        }
        return props;
    }
}

// ---------------------------------------------------------------------------
// XML data classes (internal)
// ---------------------------------------------------------------------------

internal sealed class XmlRuleSet
{
    public string? Name;
    public string? Description;
    public List<XmlRule>? Rules;
}

internal sealed class XmlRule
{
    public string? Name;
    public string? Message;
    public string? Class;
    public string? Ref;
    public string? ExternalInfoUrl;
    public string? Since;
    public string? Description;
    public int? Priority;
    public XmlProperties? Properties;
    public List<XmlExclude>? Exclude;
}

internal sealed class XmlExclude { public string? Name; }

internal sealed class XmlProperties { public List<XmlProperty> Property = new(); }

internal sealed class XmlProperty
{
    public string? Name;
    public string? Value;
    public string? InnerValue;
}
