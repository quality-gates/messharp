using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class ReturnedStorageOriginResolver
{
    public static StorageOrigin? Origin(
        CallableOwner caller,
        IReadOnlyList<CallableOwner> owners,
        InvocationExpressionSyntax invocation,
        Func<ExpressionSyntax, StorageOrigin> actualOrigin)
    {
        var target = new CallResolver(owners).Resolve(caller, invocation);
        if (target is null) return null;
        return ReturnedExpressions(target.Body).Select(returned => Instantiate(target, invocation, returned, actualOrigin))
            .Aggregate((StorageOrigin?)null, (current, origin) => origin is null ? current : current?.Join(origin) ?? origin);
    }

    private static StorageOrigin? Instantiate(
        CallableOwner target,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax returned,
        Func<ExpressionSyntax, StorageOrigin> actualOrigin)
    {
        var parameterNames = StateNames.Parameters(target.Method.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal));
        var relative = new StorageOriginResolver(parameterNames.Resolve, []).Origin(returned);
        return InstantiateSet(target, invocation, relative.Outer, actualOrigin, outer: true)
            .Join(InstantiateSet(target, invocation, relative.Reachable, actualOrigin, outer: false));
    }

    private static StorageOrigin InstantiateSet(
        CallableOwner target,
        InvocationExpressionSyntax invocation,
        IReadOnlySet<string> names,
        Func<ExpressionSyntax, StorageOrigin> actualOrigin,
        bool outer)
    {
        var result = StorageOrigin.Private;
        foreach (var name in names)
        {
            if (Actual(target, invocation, name) is not { } actual) return StorageOrigin.Private;
            var origin = actualOrigin(actual);
            result = result.Join(outer
                ? new StorageOrigin(origin.Outer, new HashSet<string>())
                : new StorageOrigin(new HashSet<string>(), origin.Reachable));
        }
        return result;
    }

    private static ExpressionSyntax? Actual(CallableOwner target, InvocationExpressionSyntax invocation, string name)
    {
        var parameter = target.Method.Parameters.FindIndex(p => p.Name == name);
        var ownCount = target.Parent is null ? target.Method.Parameters.Count
            : CallableOwner.Parameters(target.Method.Node).Count();
        if (parameter >= ownCount) return Microsoft.CodeAnalysis.CSharp.SyntaxFactory.IdentifierName(name);
        var named = invocation.ArgumentList.Arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == name);
        if (named is not null) return named.Expression;
        return parameter >= 0 && parameter < invocation.ArgumentList.Arguments.Count
            ? invocation.ArgumentList.Arguments[parameter].Expression : null;
    }

    private static IEnumerable<ExpressionSyntax> ReturnedExpressions(SyntaxNode? body)
    {
        if (body is null) yield break;
        if (body is ExpressionSyntax expression)
        {
            yield return CallableSyntax.Unwrap(expression);
            yield break;
        }
        foreach (var statement in body.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>())
            if (statement.Expression is { } returned)
                yield return CallableSyntax.Unwrap(returned);
    }
}
