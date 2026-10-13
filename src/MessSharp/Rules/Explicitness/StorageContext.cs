using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed record StorageContext(SyntaxNode Body, Func<ExpressionSyntax, string?> Root,
    CallableOwner? Owner, IReadOnlyList<CallableOwner> Owners, string Scope, HashSet<CallableOwner> Visiting, bool Weak = false)
{
    public const string ThisBinding = "$this";

    public string Identity(SyntaxNode site) => Scope + ":" + site.SpanStart + ":" + site.Span.End + ":" + site.RawKind;

    public StorageContext For(CallableOwner target, SyntaxNode body, SyntaxNode call, bool weak) =>
        new(body, expression => StorageCallBindings.Root(target, expression), target, Owners,
            Identity(call) + "/" + target.Method.Node.SpanStart, Visiting, Weak || weak);
}
