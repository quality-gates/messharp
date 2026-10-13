using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class UnresolvedExecution
{
    public static void Add(CallableOwner owner, EffectSummary summary)
    {
        if (owner.Body is null) return;
        foreach (var node in BodyWalk.Nodes(owner.Body))
            if (Operation(owner, node) is { } operation)
                summary.Add(new EffectFact(EffectKind.Unknown, "unresolved " + operation), node);
    }

    private static string? Operation(CallableOwner owner, SyntaxNode node) => node switch
    {
        RefExpressionSyntax reference when !StorageReferences.Supports(owner, reference.Expression) => "reference location",
        AssignmentExpressionSyntax assignment when CapturedAssignments.IsRebinding(owner, assignment) => "captured binding overwrite",
        IdentifierNameSyntax name when PropertyDispatch.IsBare(owner, name) => "property access '" + name + "'",
        MemberAccessExpressionSyntax member when PropertyDispatch.IsMember(owner, member) => "property access '" + member + "'",
        MemberBindingExpressionSyntax member when PropertyDispatch.IsConditional(owner, member) => "conditional property access '" + member + "'",
        ElementAccessExpressionSyntax element when PropertyDispatch.IsIndexer(owner, element.Expression) => "indexer dispatch",
        ElementBindingExpressionSyntax element when ConditionalIndexer(owner, element) => "conditional indexer dispatch",
        _ when OperatorDispatch.RequiresExecution(owner, node) => "operator dispatch",
        _ => null,
    };

    private static bool ConditionalIndexer(CallableOwner owner, ElementBindingExpressionSyntax element)
    {
        var receiver = element.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression;
        return receiver is null || PropertyDispatch.IsIndexer(owner, receiver);
    }
}
