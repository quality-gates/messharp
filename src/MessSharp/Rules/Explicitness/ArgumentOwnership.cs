using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ArgumentOwnership
{
    private readonly StorageLocations _locations;
    private readonly Func<ExpressionSyntax, string?> _parameterName;

    public ArgumentOwnership(SyntaxNode body, Func<ExpressionSyntax, string?> parameterName,
        CallableOwner? callableOwner = null, IReadOnlyList<CallableOwner>? callableOwners = null,
        HashSet<CallableOwner>? visiting = null)
    {
        _parameterName = parameterName;
        _locations = new StorageLocations(new StorageContext(body, parameterName, callableOwner, callableOwners ?? [],
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
        if (!IsAssignmentTarget(expression) || expression is not IdentifierNameSyntax) return Roots(StorageAt(expression), false);
        if (IsRefRebinding(expression)) return [];
        return LocationAt(expression, expression) is { } location ? Roots(location, true) : Roots(VariableOrigin(expression), false);
    }

    private static IReadOnlyCollection<ResolvedState> Roots(StorageOrigin origin, bool location) =>
        origin.Outer.Select(root => new ResolvedState(root, origin.Deep.Contains(root), location)).ToList();

    private StorageOrigin AssignmentOrigin(ExpressionSyntax expression) => IsRefRebinding(expression)
        ? StorageOrigin.Private : LocationAt(expression, expression) ?? VariableOrigin(expression);

    private StorageOrigin VariableOrigin(ExpressionSyntax expression) =>
        _parameterName(expression) is { } parameter ? StorageOrigin.Shared(parameter) : StorageOrigin.Private;

    public StorageOrigin StorageAt(ExpressionSyntax expression, SyntaxNode site) => _locations.Value(expression, site);

    /// <summary>
    /// The objects holding the field, element or <c>ref</c> local target that <paramref name="expression"/> names;
    /// null when it names a variable, whose write rebinds it rather than changing an object.
    /// </summary>
    public StorageOrigin? LocationAt(ExpressionSyntax expression, SyntaxNode site) =>
        _parameterName(expression) is null ? _locations.Holder(expression, site) : null;

    private static bool IsAssignmentTarget(ExpressionSyntax expression) =>
        expression.Parent is AssignmentExpressionSyntax assignment && assignment.Left == expression;

    private static bool IsRefRebinding(ExpressionSyntax expression) =>
        expression.Parent is AssignmentExpressionSyntax { Right: RefExpressionSyntax };

}
