namespace MessSharp.Rules.Explicitness;

internal sealed record StorageCallable(CallableOwner Target, IReadOnlyDictionary<string, StorageOrigin> Captures);
