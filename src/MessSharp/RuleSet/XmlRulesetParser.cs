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
        rule.Name        = def.Name ?? "";
        rule.Message     = def.Message?.Trim() ?? "";
        rule.SetName     = setName;
        rule.ExternalUrl = def.ExternalInfoUrl ?? "";
        rule.Since       = def.Since ?? "";
        rule.Description = def.Description?.Trim() ?? "";
        rule.Priority    = ov.Priority ?? def.Priority ?? 3;
        rule.RuleProps   = MergeProps(def.Properties, ov.Properties);
    }

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

    private static void ParseRuleSet(XmlReader r, XmlRuleSet xrs)
    {
        while (r.Read())
        {
            if (r.NodeType != XmlNodeType.Element) continue;
            switch (r.LocalName)
            {
                case "ruleset":
                    xrs.Name = r.GetAttribute("name");
                    break;
                case "description" when xrs.Rules == null:
                    xrs.Description = r.ReadElementContentAsString();
                    break;
                case "rule":
                    (xrs.Rules ??= new()).Add(ParseRule(r));
                    break;
            }
        }
    }

    internal static XmlRule ParseRule(XmlReader r)
    {
        var rule = new XmlRule
        {
            Name            = r.GetAttribute("name"),
            Message         = r.GetAttribute("message"),
            Class           = r.GetAttribute("class"),
            Ref             = r.GetAttribute("ref"),
            ExternalInfoUrl = r.GetAttribute("externalInfoUrl"),
            Since           = r.GetAttribute("since"),
        };

        if (r.IsEmptyElement) return rule;

        int depth = r.Depth;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == depth) break;
            if (r.NodeType != XmlNodeType.Element) continue;
            ApplyRuleChild(r, rule);
        }
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

    private static XmlProperties ParseProperties(XmlReader r)
    {
        var props = new XmlProperties { Property = new() };
        if (r.IsEmptyElement) return props;
        int depth = r.Depth;
        while (r.Read())
        {
            if (r.NodeType == XmlNodeType.EndElement && r.Depth == depth) break;
            if (r.NodeType == XmlNodeType.Element && r.LocalName == "property")
                props.Property.Add(ParseProperty(r));
        }
        return props;
    }

    private static XmlProperty ParseProperty(XmlReader r)
    {
        var p = new XmlProperty
        {
            Name  = r.GetAttribute("name"),
            Value = r.GetAttribute("value"),
        };
        if (!r.IsEmptyElement)
        {
            int pd = r.Depth;
            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.EndElement && r.Depth == pd) break;
                if (r.NodeType == XmlNodeType.Element && r.LocalName == "value")
                    p.InnerValue = r.ReadElementContentAsString();
            }
        }
        return p;
    }
}
