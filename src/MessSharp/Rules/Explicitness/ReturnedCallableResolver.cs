using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class ReturnedCallableResolver
{
    public static CallableOwner? Find(CallableOwner owner)
    {
        foreach (var returned in ReturnedExpressions(owner.Body))
        {
            var value = Unwrap(returned);
            var direct = owner.Children.FirstOrDefault(o => o.Method.Node == value);
            if (direct is not null) return direct;
            if (value is IdentifierNameSyntax name && NamedReturn(owner, name) is { } named)
                return named;
        }
        return null;
    }

    private static CallableOwner? NamedReturn(CallableOwner owner, IdentifierNameSyntax name)
    {
        var local = owner.Children.FirstOrDefault(o => o.Method.Node is LocalFunctionStatementSyntax function
            && function.Identifier.Text == name.Identifier.Text);
        if (local is not null) return local;
        if (ExpressionTypes.Binding(name)?.Initializer is not { } initializer) return null;
        return owner.Children.FirstOrDefault(o => o.Method.Node == Unwrap(initializer.Value));
    }

    private static IEnumerable<ExpressionSyntax> ReturnedExpressions(SyntaxNode? body)
    {
        if (body is null) yield break;
        if (body is ExpressionSyntax expression)
        {
            yield return expression;
            yield break;
        }
        foreach (var statement in BodyWalk.Nodes(body).OfType<ReturnStatementSyntax>())
            if (statement.Expression is { } returned)
                yield return returned;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax grouped => Unwrap(grouped.Expression),
        CastExpressionSyntax cast => Unwrap(cast.Expression),
        _ => expression,
    };
}
