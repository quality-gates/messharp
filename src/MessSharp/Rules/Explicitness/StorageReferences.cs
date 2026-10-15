using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageReferences(StorageContext context, Func<ExpressionSyntax, StorageOrigin> origin, StorageEnvironment state)
{
    public bool Bind(SyntaxNode node, bool weak)
    {
        var binding = Binding(node);
        if (binding is null) return false;
        var (name, expression) = binding.Value;
        if (Target(expression) is not { } reference)
        {
            state.References.Remove(name);
            state.Bindings[name] = StorageOrigin.Unknown;
            return true;
        }
        if (weak && state.References.TryGetValue(name, out var previous)) reference = previous.Join(reference);
        state.References[name] = reference;
        state.Bindings.Remove(name);
        return true;
    }

    private static (string Name, ExpressionSyntax Expression)? Binding(SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax { Initializer.Value: RefExpressionSyntax reference } declaration =>
            (declaration.Identifier.Text, reference.Expression),
        AssignmentExpressionSyntax { Left: IdentifierNameSyntax name, Right: RefExpressionSyntax reference } =>
            (name.Identifier.Text, reference.Expression),
        _ => null,
    };

    public StorageReference? Target(ExpressionSyntax expression)
    {
        expression = CallableSyntax.Unwrap(expression);
        if (expression is IdentifierNameSyntax name && state.References.TryGetValue(name.Identifier.Text, out var bound)) return bound;
        if (StorageMembers.OwnField(context, expression) is { } own
            && state.Bindings.TryGetValue(StorageContext.ThisBinding, out var receiver)) return StorageReference.From(receiver, own);
        return expression switch
        {
            MemberAccessExpressionSyntax member when StorageMembers.IsField(context, member) =>
                StorageReference.From(origin(member.Expression), "." + member.Name.Identifier.Text),
            ElementAccessExpressionSyntax element => Element(element),
            _ => null,
        };
    }

    private StorageReference? Element(ElementAccessExpressionSyntax element) => context.Owner is { } owner
        && !PropertyDispatch.IsIndexer(owner, element.Expression)
            ? StorageReference.From(origin(element.Expression), StorageMembers.Index(element.ArgumentList)) : null;

    public static bool Supports(CallableOwner owner, ExpressionSyntax expression)
        => Supports(owner, expression, []);

    private static bool Supports(CallableOwner owner, ExpressionSyntax expression, HashSet<ExpressionSyntax> visiting)
    {
        if (!visiting.Add(expression) || owner.Body is not { } body) return false;
        var context = new StorageContext(body, _ => null, owner, [], "", []);
        if (expression is IdentifierNameSyntax name && ExpressionTypes.Binding(name)?.Initializer?.Value is RefExpressionSyntax reference)
            return Supports(owner, reference.Expression, visiting);
        return new StorageReferences(context, _ => StorageOrigin.Private, new StorageEnvironment()).Target(expression) is not null;
    }
}
