using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageCallables(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    public StorageCallable? Find(ExpressionSyntax actual)
    {
        actual = CallableSyntax.Unwrap(actual);
        if (actual is IdentifierNameSyntax name && Bound(name) is { } bound) return bound;
        if (context.Owner is not { } owner || new CallbackResolver(context.Owners).Find(owner, actual) is not { } target) return null;
        return Capture(target);
    }

    /// <summary>
    /// The callable a local or parameter holds. A callable declared in this body reads its captured variables
    /// where it is used, since a later write to them is visible to it.
    /// </summary>
    public StorageCallable? Bound(IdentifierNameSyntax name)
    {
        var local = RefAliasCache.For(context.Body).Canonical(name.Identifier.Text);
        if (!state.Callables.TryGetValue(local, out var bound)) return null;
        return bound.Target.Parent?.Body is { } declaring && declaring.SyntaxTree == name.SyntaxTree
            && declaring.Span.Contains(name.Span) ? Capture(bound.Target) : bound;
    }

    public StorageCallable Capture(CallableOwner target)
    {
        var ownCount = target.Parent is null ? target.Method.Parameters.Count : CallableOwner.Parameters(target.Method.Node).Count();
        var captures = target.Method.Parameters.Skip(ownCount).ToDictionary(parameter => parameter.Name,
            parameter => origin(SyntaxFactory.IdentifierName(parameter.Name)), StringComparer.Ordinal);
        var references = state.References.Where(pair => captures.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return new StorageCallable(target, captures) { References = references };
    }
}
