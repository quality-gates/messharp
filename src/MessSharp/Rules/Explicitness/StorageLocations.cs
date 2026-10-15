using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The storage an expression reaches at a site of a body, as a value or as a written location.</summary>
internal sealed class StorageLocations(StorageContext context)
{
    private readonly StorageFlow _flow = new(context);

    public StorageOrigin Value(ExpressionSyntax expression, SyntaxNode site)
    {
        var state = _flow.Before(site, new StorageEnvironment());
        return state.Heap.Materialize(_flow.Origin(expression, state));
    }

    /// <summary>The objects holding the field, element or <c>ref</c> local target; null when it names a variable.</summary>
    public StorageOrigin? Holder(ExpressionSyntax expression, SyntaxNode site)
    {
        var state = _flow.Before(site, new StorageEnvironment());
        var location = new StorageReferences(context, item => _flow.Origin(item, state), state).Target(expression);
        return location is null ? null : state.Heap.Materialize(location.Receiver);
    }
}
