using System.Xml;

namespace MessSharp.RuleSet;

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
    public string? InvalidPriorityText;
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

// ---------------------------------------------------------------------------
// XML helpers (property merging, exclude sets)
// ---------------------------------------------------------------------------

/// <summary>
/// Utility helpers for XML rule properties, reused by Loader to avoid
/// direct dependencies on XmlProperty/XmlProperties/XmlExclude in Loader.
/// </summary>
internal static class XmlRuleHelpers
{
    internal static HashSet<string> ExcludeSet(List<XmlExclude>? excludes)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (excludes != null)
            foreach (var e in excludes)
                if (e.Name != null) set.Add(e.Name);
        return set;
    }

    internal static MessSharp.Rule.Properties MergeProps(XmlProperties? baseProps, XmlProperties? overProps)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        CopyProps(baseProps, map);
        CopyProps(overProps, map);
        return new MessSharp.Rule.Properties(map);
    }

    internal static void PopulateRule(MessSharp.Rule.BaseRule rule, string setName, XmlRule def, XmlRule ov)
    {
        rule.Name = def.Name ?? "";
        rule.Message = Fallback(ov.Message, def.Message)?.Trim() ?? "";
        rule.SetName = setName;
        rule.ExternalUrl = Fallback(ov.ExternalInfoUrl, def.ExternalInfoUrl) ?? "";
        rule.Since = Fallback(ov.Since, def.Since) ?? "";
        rule.Description = Fallback(ov.Description, def.Description)?.Trim() ?? "";
        rule.Priority = ov.Priority ?? def.Priority ?? 3;
        rule.RuleProps = MergeProps(def.Properties, ov.Properties);
    }

    internal static string? EffectiveInvalidPriorityText(XmlRule def, XmlRule ov) =>
        ov.Priority.HasValue ? ov.InvalidPriorityText : def.InvalidPriorityText;

    private static void CopyProps(XmlProperties? src, Dictionary<string, string> dest)
    {
        if (src?.Property == null) return;
        foreach (var p in src.Property)
            dest[p.Name ?? ""] = PropValue(p);
    }

    private static string PropValue(XmlProperty p)
    {
        if (string.IsNullOrEmpty(p.Value) && !string.IsNullOrEmpty(p.InnerValue))
            return p.InnerValue.Trim();
        return p.Value ?? "";
    }

    internal static XmlRule MergeChildRef(XmlRule child, XmlRule parent) =>
        new()
        {
            Name = Fallback(child.Name, parent.Name),
            Message = Fallback(parent.Message, child.Message),
            Class = Fallback(child.Class, parent.Class),
            Ref = child.Ref,
            ExternalInfoUrl = Fallback(parent.ExternalInfoUrl, child.ExternalInfoUrl),
            Since = Fallback(parent.Since, child.Since),
            Description = Fallback(parent.Description, child.Description),
            Priority = parent.Priority ?? child.Priority,
            InvalidPriorityText = parent.Priority.HasValue
                ? parent.InvalidPriorityText
                : child.InvalidPriorityText,
            Properties = MergeXmlProperties(child.Properties, parent.Properties),
            Exclude = CombineExcludes(child.Exclude, parent.Exclude),
        };

    private static string? Fallback(string? primary, string? secondary) =>
        primary ?? secondary;

    private static List<XmlExclude> CombineExcludes(List<XmlExclude>? first, List<XmlExclude>? second)
    {
        var result = new List<XmlExclude>();
        if (first != null) result.AddRange(first);
        if (second != null) result.AddRange(second);
        return result;
    }

    internal static XmlProperties? MergeXmlProperties(XmlProperties? baseProps, XmlProperties? overProps)
    {
        if (baseProps == null && overProps == null) return null;
        var result = new XmlProperties();
        if (baseProps?.Property != null) result.Property.AddRange(baseProps.Property);
        if (overProps?.Property != null) result.Property.AddRange(overProps.Property);
        return result;
    }
}

// ---------------------------------------------------------------------------
// XML deserializer
// ---------------------------------------------------------------------------

/// <summary>
/// Parses phpmd-format ruleset XML bytes into XmlRuleSet data objects.
/// Extracted from Loader to separate XML deserialization from rule construction.
/// </summary>
internal static class XmlRulesetParser
{
    internal static XmlRuleSet Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
        using var reader = XmlReader.Create(ms, settings);
        var xrs = new XmlRuleSet();
        ParseRuleSet(reader, xrs);
        return xrs;
    }

    /// <summary>Where the reader is in a ruleset: still in the header, or past the first rule.</summary>
    private enum RuleSetPart { Header, Rules }

    private static void ParseRuleSet(XmlReader r, XmlRuleSet xrs)
    {
        if (r.MoveToContent() != XmlNodeType.Element || r.LocalName != "ruleset") return;
        xrs.Name = r.GetAttribute("name");
        var part = RuleSetPart.Header;
        EachChild(r, child =>
        {
            switch (child.LocalName)
            {
                case "description" when part == RuleSetPart.Header:
                    xrs.Description = child.ReadElementContentAsString();
                    break;
                case "rule":
                    part = RuleSetPart.Rules;
                    (xrs.Rules ??= new()).Add(ParseRule(child));
                    break;
            }
        });
    }

    /// <summary>
    /// Calls <paramref name="visit"/> on each element directly inside the current element, then leaves the reader on its end.
    /// Each child gets its own sub-reader, so a visitor that reads text to the end tag cannot make us skip the next sibling.
    /// </summary>
    private static void EachChild(XmlReader r, Action<XmlReader> visit)
    {
        if (r.IsEmptyElement) return;
        int depth = r.Depth;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == depth) break;
            if (r.NodeType != XmlNodeType.Element) continue;
            using var sub = r.ReadSubtree();
            sub.Read();
            visit(sub);
        }
    }

    internal static XmlRule ParseRule(XmlReader r)
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
        EachChild(r, child => ApplyRuleChild(child, rule));
        return rule;
    }

    private static void ApplyRuleChild(XmlReader r, XmlRule rule)
    {
        switch (r.LocalName)
        {
            case "description":
                rule.Description = r.ReadElementContentAsString().Trim();
                break;
            case "priority":
                var raw = r.ReadElementContentAsString();
                if (int.TryParse(raw, out var p))
                {
                    rule.Priority = p;
                    rule.InvalidPriorityText = null;
                }
                else
                {
                    rule.Priority = 0;
                    rule.InvalidPriorityText = raw;
                }
                break;
            case "properties":
                rule.Properties = ParseProperties(r);
                break;
            case "exclude":
                (rule.Exclude ??= new()).Add(new XmlExclude { Name = r.GetAttribute("name") });
                break;
        }
    }

    private static XmlProperties ParseProperties(XmlReader r)
    {
        var props = new XmlProperties { Property = new() };
        EachChild(r, child =>
        {
            if (child.LocalName == "property") props.Property.Add(ParseProperty(child));
        });
        return props;
    }

    private static XmlProperty ParseProperty(XmlReader r)
    {
        var p = new XmlProperty
        {
            Name = r.GetAttribute("name"),
            Value = r.GetAttribute("value"),
        };
        EachChild(r, child =>
        {
            if (child.LocalName == "value") p.InnerValue = child.ReadElementContentAsString();
        });
        return p;
    }
}
