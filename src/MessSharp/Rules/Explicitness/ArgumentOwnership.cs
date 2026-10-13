using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ArgumentOwnership
{
    private readonly StorageFlow _flow;
    private readonly Func<ExpressionSyntax, string?> _parameterName;

    public ArgumentOwnership(SyntaxNode body, Func<ExpressionSyntax, string?> parameterName,
        CallableOwner? callableOwner = null, IReadOnlyList<CallableOwner>? callableOwners = null,
        HashSet<CallableOwner>? visiting = null)
    {
        _parameterName = parameterName;
        _flow = new StorageFlow(new StorageContext(body, parameterName, callableOwner, callableOwners ?? [],
            body.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture), visiting ?? []));
    }

    public string? Resolve(ExpressionSyntax expression)
    {
        if (expression is not IdentifierNameSyntax and not ElementAccessExpressionSyntax and not InvocationExpressionSyntax)
            return null;
        if (IsAssignmentTarget(expression) && expression is IdentifierNameSyntax)
            return StorageRoots.Preferred(AssignmentOrigin(expression).Outer);
        return StorageRoots.Preferred(StorageAt(expression).Outer);
    }

    public StorageOrigin StorageAt(ExpressionSyntax expression) => StorageAt(expression, expression);

    public IReadOnlyCollection<ResolvedState> MutationRoots(ExpressionSyntax expression)
    {
        var origin = IsAssignmentTarget(expression) && expression is IdentifierNameSyntax
            ? AssignmentOrigin(expression) : StorageAt(expression);
        return origin.Outer.Select(root => new ResolvedState(root, origin.Deep.Contains(root))).ToList();
    }

    private StorageOrigin AssignmentOrigin(ExpressionSyntax expression)
    {
        if (expression.Parent is AssignmentExpressionSyntax { Right: RefExpressionSyntax }) return StorageOrigin.Private;
        var state = _flow.Before(expression, new StorageEnvironment());
        if (expression is IdentifierNameSyntax name && state.References.TryGetValue(name.Identifier.Text, out var reference))
        {
            var receiver = state.Heap.Materialize(reference.Receiver);
            return receiver with { Deep = receiver.Deep.Union(receiver.Outer).ToHashSet() };
        }
        return _parameterName(expression) is { } parameter ? StorageOrigin.Shared(parameter) : StorageOrigin.Private;
    }

    public StorageOrigin StorageAt(ExpressionSyntax expression, SyntaxNode site)
    {
        var state = _flow.Before(site, new StorageEnvironment());
        return state.Heap.Materialize(_flow.Origin(expression, state));
    }

    private static bool IsAssignmentTarget(ExpressionSyntax expression) =>
        expression.Parent is AssignmentExpressionSyntax assignment && assignment.Left == expression;

}
