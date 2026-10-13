using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ArgumentOwnership
{
    private readonly BindingOrigins _bindings;
    private readonly Func<ExpressionSyntax, string?> _parameterName;
    private readonly CallableOwner? _callableOwner;
    private readonly IReadOnlyList<CallableOwner>? _callableOwners;
    private readonly HashSet<CallableOwner>? _visiting;

    public ArgumentOwnership(SyntaxNode body, Func<ExpressionSyntax, string?> parameterName,
        CallableOwner? callableOwner = null, IReadOnlyList<CallableOwner>? callableOwners = null,
        HashSet<CallableOwner>? visiting = null)
    {
        _parameterName = parameterName;
        _callableOwner = callableOwner;
        _callableOwners = callableOwners;
        _visiting = visiting;
        _bindings = new BindingOrigins(body, Origin);
    }

    public string? Resolve(ExpressionSyntax expression)
    {
        if (expression is not IdentifierNameSyntax and not ElementAccessExpressionSyntax and not InvocationExpressionSyntax)
            return null;
        if (IsAssignmentTarget(expression) && expression is IdentifierNameSyntax)
            return _parameterName(expression);
        return StorageRoots.Preferred(StorageAt(expression).Outer);
    }

    public StorageOrigin StorageAt(ExpressionSyntax expression) => StorageAt(expression, expression);

    public IReadOnlyCollection<ResolvedState> MutationRoots(ExpressionSyntax expression)
    {
        if (IsAssignmentTarget(expression) && expression is IdentifierNameSyntax)
            return _parameterName(expression) is { } name ? [new ResolvedState(name)] : [];
        var origin = StorageAt(expression);
        return origin.Outer.Select(root => new ResolvedState(root, origin.Deep.Contains(root))).ToList();
    }

    public StorageOrigin StorageAt(ExpressionSyntax expression, SyntaxNode site) =>
        Origin(expression, _bindings.Before(site));

    private StorageOrigin Origin(ExpressionSyntax expression, Dictionary<string, StorageOrigin> owners) =>
        Resolver(owners).Origin(expression);

    private StorageOriginResolver Resolver(Dictionary<string, StorageOrigin> owners)
    {
        StorageOrigin? InvocationOrigin(InvocationExpressionSyntax invocation) =>
            _callableOwner is null || _callableOwners is null
                ? null : ReturnedStorageOriginResolver.Origin(_callableOwner, _callableOwners, invocation,
                    expression => Origin(expression, owners), _visiting);
        return new StorageOriginResolver(_parameterName, owners, InvocationOrigin);
    }

    private static bool IsAssignmentTarget(ExpressionSyntax expression) =>
        expression.Parent is AssignmentExpressionSyntax assignment && assignment.Left == expression;

}
