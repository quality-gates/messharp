using MessSharp.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class CallArguments
{
    public static ExpressionSyntax? Actual(CallEdge edge, string name)
    {
        var parameter = edge.Target.Method.Parameters.FindIndex(p => p.Name == name);
        var ownCount = edge.Target.Parent is null ? edge.Target.Method.Parameters.Count
            : CallableOwner.Parameters(edge.Target.Method.Node).Count();
        if (parameter >= ownCount) return SyntaxFactory.IdentifierName(name);
        return Actual(edge.Call, name, parameter);
    }

    public static ExpressionSyntax? Actual(InvocationExpressionSyntax call, string name, int position)
    {
        var named = call.ArgumentList.Arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == name);
        if (named is not null) return named.Expression;
        return position >= 0 && position < call.ArgumentList.Arguments.Count
            ? call.ArgumentList.Arguments[position].Expression : null;
    }

    public static StorageOrigin Storage(RuleContext ctx, CallEdge edge, IReadOnlyList<CallableOwner> owners, string name,
        SyntaxNode body)
    {
        if (edge.Bindings?.TryGetValue(name, out var bound) == true) return bound;
        return Actual(edge, name) is { } actual ? Ownership(ctx, edge.Caller, owners, body).StorageAt(actual, edge.Site)
            : StorageOrigin.Unknown;
    }

    public static StorageOrigin Project(RuleContext ctx, CallEdge edge, IReadOnlyList<CallableOwner> owners,
        StorageOrigin relative, SyntaxNode body) => StorageProjection.Apply(relative,
        root => root.Contains(':', StringComparison.Ordinal) || root == StorageOrigin.UnknownRoot
            ? StorageOrigin.Shared(root) : Storage(ctx, edge, owners, root, body));

    public static ArgumentOwnership Ownership(RuleContext ctx, CallableOwner owner, IReadOnlyList<CallableOwner> owners,
        SyntaxNode body)
    {
        var roots = new StateRootResolver(ctx, owner);
        return new ArgumentOwnership(body, roots.Resolve, owner, owners);
    }
}
