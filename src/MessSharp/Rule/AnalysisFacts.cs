namespace MessSharp.Rule;

/// <summary>
/// Facts that rules derive from an artifact, shared by every rule of one
/// engine analysis so each fact is derived once. A new analysis starts empty.
/// Subjects compare by identity: two equal-looking artifacts are still
/// different artifacts.
/// </summary>
internal sealed class AnalysisFacts
{
    private readonly Dictionary<Type, Dictionary<object, object>> _facts = [];

    /// <summary>The fact of type T about the subject, derived on first use.</summary>
    public T Get<T>(object subject, Func<T> derive) where T : class
    {
        if (!_facts.TryGetValue(typeof(T), out var bySubject))
            _facts[typeof(T)] = bySubject = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        if (bySubject.TryGetValue(subject, out var fact)) return (T)fact;
        var derived = derive();
        bySubject[subject] = derived;
        return derived;
    }
}
