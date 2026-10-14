using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// The elements a collection can hold when read: the items of a literal array or collection initializer,
/// or of a body-local array whose only uses are element reads and element writes. Anything else is unknown.
/// </summary>
internal static class DeferredElements
{
    public static IReadOnlyList<ExpressionSyntax>? ElementsOf(DeferredValueContext context, ExpressionSyntax collection)
    {
        collection = CallableSyntax.Unwrap(collection);
        return Literal(collection) ?? Local(context, collection);
    }

    /// <summary>A body-local array's elements: the items of every literal assigned to it and its element writes.</summary>
    private static IReadOnlyList<ExpressionSyntax>? Local(DeferredValueContext context, ExpressionSyntax collection)
    {
        if (Declaration(context, collection) is not { } declaration || context.Values(collection) is not { } sources) return null;
        var elements = Assigned(sources);
        return elements is not null && Writes(context.Body, declaration, elements) ? elements : null;
    }

    private static SyntaxNode? Declaration(DeferredValueContext context, ExpressionSyntax collection) =>
        collection is IdentifierNameSyntax name && ExpressionTypes.Binding(name) is { } declaration
            && context.Body.Span.Contains(declaration.Span) ? declaration : null;

    private static List<ExpressionSyntax>? Assigned(IEnumerable<DeferredSource> sources)
    {
        var elements = new List<ExpressionSyntax>();
        foreach (var source in sources)
        {
            if (Held(source) is not { } held) return null;
            elements.AddRange(held);
        }
        return elements;
    }

    private static IReadOnlyList<ExpressionSyntax>? Held(DeferredSource source) =>
        source is DeferredSource.Value { Owner: null } value ? Literal(CallableSyntax.Unwrap(value.Expression)) : null;

    private static IReadOnlyList<ExpressionSyntax>? Literal(ExpressionSyntax collection) => collection switch
    {
        ArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions,
        ImplicitArrayCreationExpressionSyntax creation => creation.Initializer.Expressions,
        CollectionExpressionSyntax list when list.Elements.All(element => element is ExpressionElementSyntax) =>
            list.Elements.OfType<ExpressionElementSyntax>().Select(element => element.Expression).ToList(),
        BaseObjectCreationExpressionSyntax { Initializer: { } initializer }
            when initializer.IsKind(SyntaxKind.CollectionInitializerExpression)
                && initializer.Expressions.All(item => item is not InitializerExpressionSyntax) => initializer.Expressions,
        _ => null,
    };

    /// <summary>Adds element writes; false when the array escapes or is written another way.</summary>
    private static bool Writes(SyntaxNode body, SyntaxNode declaration, List<ExpressionSyntax> elements)
    {
        foreach (var use in Uses(body, declaration))
            if (!Record(use, elements)) return false;
        return true;
    }

    private static IEnumerable<IdentifierNameSyntax> Uses(SyntaxNode body, SyntaxNode declaration)
    {
        var name = declaration is VariableDeclaratorSyntax variable ? variable.Identifier.Text : "";
        return body.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.Text == name && ExpressionTypes.Binding(id) == declaration);
    }

    /// <summary>Adds the value of an element write; false when the use is not an element read or simple write.</summary>
    private static bool Record(IdentifierNameSyntax use, List<ExpressionSyntax> elements)
    {
        if (ElementAccess(use) is not { } access) return false;
        if (access.Parent is not AssignmentExpressionSyntax assignment || assignment.Left != access) return true;
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)) return false;
        elements.Add(assignment.Right);
        return true;
    }

    /// <summary>The element access <paramref name="use"/> indexes, unless that element is passed by reference.</summary>
    private static ElementAccessExpressionSyntax? ElementAccess(IdentifierNameSyntax use) =>
        use.Parent is ElementAccessExpressionSyntax access && access.Expression == use
            && access.Parent is not ArgumentSyntax { RefKindKeyword.RawKind: not (int)SyntaxKind.None } ? access : null;
}
