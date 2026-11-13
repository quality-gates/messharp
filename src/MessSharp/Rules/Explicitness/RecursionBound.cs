using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Decides whether a value already being resolved may be resolved again. The same value reached in the same
/// context is a cycle and stays unknown. Reached in another call frame it is recursion: each level binds the
/// parameters from the level above, so after one level per parameter every source has appeared and a deeper
/// level adds no work.
/// </summary>
internal static class RecursionBound
{
    private const int MaxParameters = 5;

    private static readonly DeferredResolution Unknown = new([], false);
    private static readonly DeferredResolution Nothing = new([], true);

    /// <summary>Null when the value may be resolved again; otherwise what the revisit stands for.</summary>
    public static DeferredResolution? Revisit(DeferredValueContext context, IReadOnlyList<DeferredValueContext> resolving)
    {
        if (resolving.Contains(context) || !IsRecursion(context)) return Unknown;
        var parameters = context.Owner.Method.Parameters.Count;
        if (parameters > MaxParameters) return Unknown;
        return resolving.Count > parameters ? Nothing : null;
    }

    private static bool IsRecursion(DeferredValueContext context)
    {
        if (context.Caller is null) return false;
        for (var frame = context; frame is not null; frame = frame.Caller?.Context)
            if (frame.Foreign) return false;
        return true;
    }
}
