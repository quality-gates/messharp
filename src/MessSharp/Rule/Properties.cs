using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MessSharp.Rule;

public enum PropertySetKind { Ordinal, LowerInvariantIgnoreCase }
public enum PropertyNullPolicy { Throw, Empty }

/// <summary>
/// Configurable rule properties parsed from ruleset XML.
/// Mirrors messgo's Properties map[string]string with typed accessors.
/// Structured results are retained for this instance's lifetime, including old values.
/// Concurrent reads are supported, but callers must synchronize external map writes
/// against all reads. Regex entries use the current culture name as in .NET 8 regex
/// case equivalence; this assumption must be revisited when changing runtime versions.
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
    private readonly object _cacheLock = new();
    private readonly Dictionary<(string Key, string? Raw), IReadOnlyList<string>> _lists = new();
    private readonly Dictionary<(string Key, string? Raw, PropertySetKind Kind, PropertyNullPolicy NullPolicy), IReadOnlySet<string>> _sets = new();
    private readonly Dictionary<(string Key, string? Raw, string Culture), Regex?> _regexes = new();

    public IReadOnlyList<string> List(string key, string def = "")
    {
        var raw = Str(key, def);
        lock (_cacheLock)
        {
            var request = (key, raw);
            if (!_lists.TryGetValue(request, out var value))
                _lists.Add(request, value = Array.AsReadOnly(Split(raw)));
            return value;
        }
    }

    public IReadOnlySet<string> Set(string key, string def = "", PropertySetKind kind = PropertySetKind.Ordinal,
        PropertyNullPolicy nullPolicy = PropertyNullPolicy.Throw)
    {
        var raw = Str(key, def);
        lock (_cacheLock)
        {
            var request = (key, raw, kind, nullPolicy);
            if (!_sets.TryGetValue(request, out var value))
            {
                var items = Split(raw == null && nullPolicy == PropertyNullPolicy.Empty ? "" : raw);
                value = kind == PropertySetKind.LowerInvariantIgnoreCase
                    ? items.Select(x => x.ToLowerInvariant()).ToFrozenSet(StringComparer.OrdinalIgnoreCase)
                    : items.ToFrozenSet(StringComparer.Ordinal);
                _sets.Add(request, value);
            }
            return value;
        }
    }

    public Regex? Regex(string key, string def = "")
    {
        var raw = Str(key, def);
        var culture = CultureInfo.CurrentCulture.Name;
        lock (_cacheLock)
        {
            var request = (key, raw, culture);
            if (!_regexes.TryGetValue(request, out var value))
                _regexes.Add(request, value = CompileRegex(raw));
            return value;
        }
    }

    private static string[] Split(string? raw) =>
        raw?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? throw new NullReferenceException();

    private static Regex? CompileRegex(string? pat)
    {
        if (string.IsNullOrEmpty(pat)) return null;
        string body = pat, flags = "";
        var match = System.Text.RegularExpressions.Regex.Match(pat, @"^\((.*)\)([imsxu]*)$");
        if (match.Success)
        {
            body = match.Groups[1].Value;
            flags = match.Groups[2].Value.Replace("u", "");
        }
        if (flags.Length > 0) body = "(?" + flags + ")" + body;
        try { return new Regex(body); }
        catch { return null; }
    }

    public Properties(Dictionary<string, string>? map = null)
    {
        _map = map ?? new();
    }

    /// <summary>True when both instances hold exactly the same key/value pairs.</summary>
    internal bool HasSameValuesAs(Properties other) =>
        _map.Count == other._map.Count
        && _map.All(kv => other._map.TryGetValue(kv.Key, out var v) && v == kv.Value);

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
