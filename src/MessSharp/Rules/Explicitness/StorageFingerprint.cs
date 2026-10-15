namespace MessSharp.Rules.Explicitness;

/// <summary>Canonical text for an abstract environment: equal text drives a callee to equal results.</summary>
internal sealed class StorageFingerprint
{
    private readonly FingerprintWriter _writer = new();

    public static string For(StorageEnvironment environment)
    {
        var fingerprint = new StorageFingerprint();
        fingerprint.Environment(environment);
        return fingerprint._writer.ToString();
    }

    private void Environment(StorageEnvironment environment)
    {
        _writer.Map(environment.Bindings, _writer.Origin);
        _writer.Map(environment.Calls, _writer.Origin);
        _writer.Map(environment.Callables, Callable);
        _writer.Map(environment.References, Reference);
        _writer.Map(environment.Deferred, Deferred);
        _writer.Map(environment.Heap.Contents, Cell);
    }

    private void Callable(StorageCallable callable)
    {
        _writer.Instance(callable.Target);
        _writer.Map(callable.Captures, _writer.Origin);
        _writer.Map(callable.References, Reference);
    }

    private void Reference(StorageReference reference) => _writer.Map(reference.Locations, _writer.Origin);

    private void Deferred(StorageDeferredValue value)
    {
        _writer.Count('k', (int)value.Kind);
        _writer.Count('s', (int)value.Status);
        _writer.Count('u', value.UsesSource ? 1 : 0);
        _writer.Optional(value.Source, _writer.Origin);
        _writer.Count('[', value.Work.Count);
        foreach (var work in value.Work) Work(work);
    }

    private void Work(StorageDeferredWork work)
    {
        _writer.Instance(work.Target);
        Environment(work.Initial);
        _writer.Optional(work.ElementParameters, _writer.Names);
    }

    private void Cell(StorageCell cell)
    {
        _writer.Origin(cell.Default);
        _writer.Optional(cell.Length?.ToString(System.Globalization.CultureInfo.InvariantCulture), _writer.Text);
        _writer.Map(cell.Slots, _writer.Origin);
        _writer.Names(cell.Appends);
    }
}
