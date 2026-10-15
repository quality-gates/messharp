using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

/// <summary>How a call through a delegate binds the callable the delegate holds.</summary>
internal static class HeldCallables
{
    /// <summary>
    /// The captured variables a held callable runs with at <paramref name="site"/>. Inside the body that declares
    /// the callable each captured variable holds its value at the site, so none is bound; elsewhere the callable
    /// runs with what the delegate captured.
    /// </summary>
    public static Dictionary<string, StorageOrigin> Captured(this DeferredWork work, SyntaxNode site) =>
        work.Target.Parent?.Body is { } body && body.SyntaxTree == site.SyntaxTree && body.Span.Contains(site.Span)
            ? new(StringComparer.Ordinal) : new(work.Bindings, StringComparer.Ordinal);

    /// <summary>The parameters a caller passes: a nested callable's captured variables are not among them.</summary>
    public static IEnumerable<string> OwnParameters(this CallableOwner target) =>
        target.Method.Parameters.Take(target.Parent is null ? target.Method.Parameters.Count
            : CallableOwner.Parameters(target.Method.Node).Count()).Select(parameter => parameter.Name);
}
