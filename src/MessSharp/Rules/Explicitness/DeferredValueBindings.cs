using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The sources a referenced variable, parameter, field or property can hold at a read site: flow-sensitive
/// within the reading body, plus every write the body does not order.
/// </summary>
internal sealed class DeferredValueBindings(CallableOwner owner, SyntaxNode body, IReadOnlyList<CallableOwner> owners)
{
    private readonly DeferredDeclarations _declarations = new(owner);

    public IReadOnlyList<DeferredSource>? Values(ExpressionSyntax reference)
    {
        var declaration = _declarations.Find(reference);
        if (declaration is null) return new DeferredGetters(owner, owners).Values(reference);
        var values = new DeclarationFlow(body, _declarations).Reaching(declaration, reference);
        var writers = new DeferredWriters(owner, body, owners, _declarations);
        values.AddRange(writers.Outside(declaration));
        values.AddRange(writers.Nested(declaration));
        return values;
    }
}
