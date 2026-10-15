using System.Globalization;
using System.Text;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Appends values as unambiguous text: strings and lists carry their lengths and maps are sorted by key, so two
/// different values never print the same text.
/// </summary>
internal sealed class FingerprintWriter
{
    private readonly StringBuilder _text = new();

    public void Map<T>(IReadOnlyDictionary<string, T> map, Action<T> value)
    {
        Count('{', map.Count);
        foreach (var (key, item) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Text(key);
            value(item);
        }
        _text.Append('}');
    }

    public void Names(IEnumerable<string> names)
    {
        var sorted = names.Order(StringComparer.Ordinal).ToList();
        Count('(', sorted.Count);
        foreach (var name in sorted) Text(name);
        _text.Append(')');
    }

    public void Origin(StorageOrigin origin)
    {
        Names(origin.Outer);
        Names(origin.Reachable);
        Names(origin.Deep);
        Names(origin.Objects);
    }

    public void Optional<T>(T? value, Action<T> write) where T : class
    {
        _text.Append(value is null ? '0' : '1');
        if (value is not null) write(value);
    }

    public void Count(char kind, int count) => _text.Append(kind).Append(count.ToString(CultureInfo.InvariantCulture)).Append(';');

    public void Instance(object instance) => Text(InstanceKeys.For(instance));

    public void Text(string value) => _text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);

    public override string ToString() => _text.ToString();
}
