class Cache<T>
{
    static T? _value;
    static T? Bare() => _value;
    static T? Qualified() => Cache<T>._value;
    static void QualifiedWrite(T v) { Cache<T>._value = v; }
}
