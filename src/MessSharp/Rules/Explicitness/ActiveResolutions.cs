using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

/// <summary>The values being resolved right now, each with the contexts it is being resolved in.</summary>
internal sealed class ActiveResolutions
{
    private readonly Dictionary<SyntaxNode, List<DeferredValueContext>> _active = [];

    /// <summary>Starts resolving <paramref name="value"/>; a resolution when it must not be resolved again, else null.</summary>
    public DeferredResolution? Enter(DeferredValueContext context, SyntaxNode value)
    {
        var resolving = _active.TryGetValue(value, out var contexts) ? contexts : _active[value] = [];
        if (resolving.Count > 0 && RecursionBound.Revisit(context, resolving) is { } revisit) return revisit;
        resolving.Add(context);
        return null;
    }

    public void Leave(SyntaxNode value)
    {
        var resolving = _active[value];
        resolving.RemoveAt(resolving.Count - 1);
    }
}
