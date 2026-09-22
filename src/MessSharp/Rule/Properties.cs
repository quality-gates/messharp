namespace MessSharp.Rule;

/// <summary>
/// Configurable rule properties parsed from ruleset XML.
/// Mirrors messgo's Properties map[string]string with typed accessors.
/// </summary>
public sealed class Properties
{
    private static readonly HashSet<string> TrueValues = new(StringComparer.Ordinal)
    {
        "true",
        "1",
        "yes",
        "on",
    };

    private static readonly HashSet<string> FalseValues = new(StringComparer.Ordinal)
    {
        "false",
        "0",
        "no",
        "off",
    };

    private readonly Dictionary<string, string> _map;

    public Properties(Dictionary<string, string>? map = null)
    {
        _map = map ?? new();
    }

    public int Int(string key, int def)
    {
        if (_map.TryGetValue(key, out var v) && int.TryParse(v, out var n))
            return n;
        return def;
    }

    public double Float(string key, double def)
    {
        if (_map.TryGetValue(key, out var v)
            && double.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var n))
            return n;
        return def;
    }

    public bool Bool(string key, bool def)
    {
        if (!_map.TryGetValue(key, out var v)) return def;
        if (TrueValues.Contains(v)) return true;
        if (FalseValues.Contains(v)) return false;
        return def;
    }

    public string Str(string key, string def)
    {
        return _map.TryGetValue(key, out var v) ? v : def;
    }

    public static Properties Empty { get; } = new();
}
