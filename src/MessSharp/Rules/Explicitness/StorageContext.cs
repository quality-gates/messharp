using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed record StorageContext(SyntaxNode Body, Func<ExpressionSyntax, string?> Root,
    CallableOwner? Owner, IReadOnlyList<CallableOwner> Owners, string Scope, HashSet<CallableOwner> Visiting, bool Weak = false)
{
    public const string ThisBinding = "$this";

    public string Identity(SyntaxNode site) => Scope + ":" + site.SpanStart + ":" + site.Span.End + ":" + site.RawKind;

    /// <summary>
    /// How many nested call sites a scope remembers. Allocation identities and memo keys include the scope, so an
    /// unbounded path makes every route through a layered helper graph a distinct context (exponential work);
    /// deeper frames are forgotten and equivalent entry states share one execution.
    /// </summary>
    internal const int ScopeDepth = 4;

    public StorageContext For(CallableOwner target, SyntaxNode body, SyntaxNode call, bool weak) =>
        new(body, expression => StorageCallBindings.Root(target, expression), target, Owners,
            Extend(call, target), Visiting, Weak || weak);

    private string Extend(SyntaxNode call, CallableOwner target)
    {
        var frame = $"{call.SpanStart}:{call.Span.End}:{call.RawKind}/{target.Method.Node.SpanStart}";
        var frames = Scope.Length == 0 ? [frame] : Scope.Split(';').Append(frame).ToArray();
        return string.Join(";", frames.Skip(Math.Max(0, frames.Length - ScopeDepth)));
    }
}
