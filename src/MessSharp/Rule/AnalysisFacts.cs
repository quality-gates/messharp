namespace MessSharp.Rule;

/// <summary>
/// Facts that rules derive from an artifact, shared by every rule of one
/// engine analysis so each fact is derived once. A new analysis starts empty.
/// </summary>
public sealed class AnalysisFacts
{
    private readonly Dictionary<(Type Kind, object Subject), object> _facts =
        new(new SubjectComparer());

    /// <summary>The fact of type T about the subject, derived on first use.</summary>
    public T Get<T>(object subject, Func<T> derive) where T : class
    {
        var key = (typeof(T), subject);
        if (_facts.TryGetValue(key, out var fact)) return (T)fact;
        var derived = derive();
        _facts[key] = derived;
        return derived;
    }

    /// <summary>Compares subjects by identity: two equal-looking artifacts are still different artifacts.</summary>
    private sealed class SubjectComparer : IEqualityComparer<(Type Kind, object Subject)>
    {
        public bool Equals((Type Kind, object Subject) x, (Type Kind, object Subject) y) =>
            x.Kind == y.Kind && ReferenceEquals(x.Subject, y.Subject);

        public int GetHashCode((Type Kind, object Subject) key) =>
            HashCode.Combine(key.Kind, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key.Subject));
    }
}
