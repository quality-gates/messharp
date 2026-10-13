using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class ReturnedStorageOriginResolver
{
    public static StorageOrigin? Origin(
        CallableOwner caller,
        IReadOnlyList<CallableOwner> owners,
        InvocationExpressionSyntax invocation,
        Func<ExpressionSyntax, StorageOrigin> actualOrigin,
        HashSet<CallableOwner>? visiting = null)
    {
        var target = new CallResolver(owners).Resolve(caller, invocation);
        if (target?.Body is not { } body) return null;
        visiting ??= [];
        if (!visiting.Add(target)) return StorageOrigin.Unknown;
        try
        {
            return ReturnedExpressions(body).Select(returned => Instantiate(target, owners, invocation, returned,
                    actualOrigin, visiting, body))
                .Aggregate((StorageOrigin?)null, (current, origin) => origin is null ? current : current?.Join(origin) ?? origin);
        }
        finally { visiting.Remove(target); }
    }

    private static StorageOrigin? Instantiate(
        CallableOwner target,
        IReadOnlyList<CallableOwner> owners,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax returned,
        Func<ExpressionSyntax, StorageOrigin> actualOrigin,
        HashSet<CallableOwner> visiting,
        SyntaxNode body)
    {
        var parameterNames = StateNames.Parameters(target.Method.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal));
        string? Root(ExpressionSyntax expression) => parameterNames.Resolve(expression)
            ?? ClassRoot(target, expression);
        var relative = new ArgumentOwnership(body, Root, target, owners, visiting).StorageAt(returned);
        return InstantiateRoots(target, invocation, relative, actualOrigin);
    }

    private static StorageOrigin InstantiateRoots(
        CallableOwner target,
        InvocationExpressionSyntax invocation,
        StorageOrigin relative,
        Func<ExpressionSyntax, StorageOrigin> actualOrigin)
    {
        var result = StorageOrigin.Private;
        foreach (var name in relative.Outer.Union(relative.Reachable))
        {
            if (name.StartsWith("static:", StringComparison.Ordinal) || name.StartsWith("member:", StringComparison.Ordinal))
            {
                result = result.Join(Project(relative, name, StorageOrigin.Shared(name)));
                continue;
            }
            if (Actual(target, invocation, name) is not { } actual) return StorageOrigin.Unknown;
            var origin = actualOrigin(actual);
            result = result.Join(Project(relative, name, origin));
        }
        return result;
    }

    private static string? ClassRoot(CallableOwner target, ExpressionSyntax expression)
    {
        if (target.Method.Class is not { } cls) return null;
        var name = expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            _ => null,
        };
        var field = cls.Fields.FirstOrDefault(f => f.Name == name);
        return field is null ? null : field.IsStatic ? StorageRoots.Static(field.Name) : StorageRoots.Member(field.Name);
    }

    private static StorageOrigin Project(StorageOrigin relative, string name, StorageOrigin actual)
    {
        if (!relative.Outer.Contains(name)) return new StorageOrigin(new HashSet<string>(), actual.Reachable, new HashSet<string>());
        return relative.Deep.Contains(name) ? actual.Element() : actual;
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
        foreach (var statement in BodyWalk.Nodes(body).OfType<ReturnStatementSyntax>())
            if (statement.Expression is { } returned)
                yield return CallableSyntax.Unwrap(returned);
    }
}
