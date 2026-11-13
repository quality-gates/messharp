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
        return elements is not null && Writes(context, declaration, elements) ? elements : null;
    }

    /// <summary>The name of the parameter <paramref name="collection"/> reads when the body only reads its elements.</summary>
    public static string? ReadOnlyParameter(DeferredValueContext context, ExpressionSyntax collection)
    {
        if (CallableSyntax.Unwrap(collection) is not IdentifierNameSyntax name || ExpressionTypes.Binding(name) is not null)
            return null;
        return ElementUses.OnlyReads(context.Owner, context.Body, name.Identifier.Text) ? name.Identifier.Text : null;
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
        ObjectCreationExpressionSyntax { Type: GenericNameSyntax { Identifier.Text: "List" }, Initializer: null }
            creation when creation.ArgumentList is not { Arguments.Count: > 0 } => [],
        BaseObjectCreationExpressionSyntax { Initializer: { } initializer }
            when initializer.IsKind(SyntaxKind.CollectionInitializerExpression)
                && initializer.Expressions.All(item => item is not InitializerExpressionSyntax) => initializer.Expressions,
        _ => null,
    };

    /// <summary>Adds element writes; false when the array escapes or is written another way.</summary>
    private static bool Writes(DeferredValueContext context, SyntaxNode declaration, List<ExpressionSyntax> elements)
    {
        foreach (var use in Uses(context.Body, declaration))
            if (!ElementUses.PassedToReader(context, use) && !ElementUses.Record(use, elements)) return false;
        return true;
    }

    private static IEnumerable<IdentifierNameSyntax> Uses(SyntaxNode body, SyntaxNode declaration)
    {
        var name = declaration is VariableDeclaratorSyntax variable ? variable.Identifier.Text : "";
        return body.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.Text == name && ExpressionTypes.Binding(id) == declaration);
    }
}
