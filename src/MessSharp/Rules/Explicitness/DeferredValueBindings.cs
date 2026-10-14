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
        if (declaration is null) return LazyValues(reference) ?? new DeferredGetters(owner, owners).Values(reference);
        var values = new DeclarationFlow(body, _declarations).Reaching(declaration, reference);
        var writers = new DeferredWriters(owner, body, owners, _declarations);
        values.AddRange(writers.Outside(declaration));
        values.AddRange(writers.Nested(declaration));
        return values;
    }

    private IReadOnlyList<DeferredSource>? LazyValues(ExpressionSyntax reference)
    {
        if (reference is not MemberAccessExpressionSyntax { Name.Identifier.Text: "Value" } member
            || !KnownExecutionOperations.IsLazyValue(owner, member.Expression)) return null;
        if (Values(member.Expression) is not { Count: > 0 } sources) return [new DeferredSource.Opaque()];
        return sources.Select(LazySource).ToList();
    }

    private DeferredSource LazySource(DeferredSource source) =>
        source is DeferredSource.Value { Expression: BaseObjectCreationExpressionSyntax creation } value
            && LazyFactories.Select(value.Owner ?? owner, creation) is { Kind: LazyFactoryKind.None, Expression: { } preset }
            ? new DeferredSource.Value(preset, value.Owner) : new DeferredSource.Opaque();
}
