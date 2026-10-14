using Microsoft.CodeAnalysis.CSharp;

namespace MessSharp.Rules.Explicitness;

/// <summary>What a nested callable's captured variables hold when a context runs it.</summary>
internal static class DeferredCaptures
{
    /// <summary>
    /// Each captured variable of <paramref name="target"/>: the context's binding for it, or else its storage
    /// as seen from the context.
    /// </summary>
    public static Dictionary<string, StorageOrigin> Captures(this DeferredValueContext context, CallableOwner target) =>
        target.Method.Parameters.Skip(CallableOwner.Parameters(target.Method.Node).Count()).ToDictionary(
            parameter => parameter.Name,
            parameter => context.Bindings.GetValueOrDefault(parameter.Name)
                ?? context.Storage(SyntaxFactory.IdentifierName(parameter.Name)));
}
