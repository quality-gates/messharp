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
        return Actual(edge.Target, edge.Call, name, parameter);
    }

    /// <summary>
    /// The actual for parameter <paramref name="position"/> of <paramref name="target"/>. A reduced extension
    /// call passes its receiver as the first parameter and shifts the written arguments by one.
    /// </summary>
    public static ExpressionSyntax? Actual(CallableOwner target, InvocationExpressionSyntax call, string name, int position)
    {
        if (!IsReducedExtension(target, call)) return Actual(call, name, position);
        return position == 0 ? ((MemberAccessExpressionSyntax)call.Expression).Expression
            : Actual(call.ArgumentList, name, position - 1);
    }

    public static bool IsReducedExtension(CallableOwner target, InvocationExpressionSyntax call) =>
        ExtensionMethods.IsExtension(target) && call.Expression is MemberAccessExpressionSyntax member
        && !(member.Expression is IdentifierNameSyntax type && type.Identifier.Text == target.Method.Class?.Name);

    public static ExpressionSyntax? Actual(InvocationExpressionSyntax call, string name, int position) =>
        Actual(call.ArgumentList, name, position);

    public static ExpressionSyntax? Actual(ArgumentListSyntax arguments, string name, int position)
    {
        var named = arguments.Arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == name);
        if (named is not null) return named.Expression;
        return position >= 0 && position < arguments.Arguments.Count
            ? arguments.Arguments[position].Expression : null;
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
