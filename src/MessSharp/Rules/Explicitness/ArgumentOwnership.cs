using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ArgumentOwnership(
    SyntaxNode body,
    Func<ExpressionSyntax, string?> parameterName,
    CallableOwner? callableOwner = null,
    IReadOnlyList<CallableOwner>? callableOwners = null)
{
    public string? Resolve(ExpressionSyntax expression)
    {
        if (expression is not IdentifierNameSyntax and not ElementAccessExpressionSyntax and not InvocationExpressionSyntax)
            return null;
        if (IsAssignmentTarget(expression) && expression is IdentifierNameSyntax)
            return parameterName(expression);
        return StorageAt(expression).Outer.FirstOrDefault();
    }

    public StorageOrigin StorageAt(ExpressionSyntax expression) => StorageAt(expression, expression);

    public StorageOrigin StorageAt(ExpressionSyntax expression, SyntaxNode site)
    {
        var owners = new Dictionary<string, StorageOrigin>(StringComparer.Ordinal);
        foreach (var node in BodyWalk.Nodes(body).Where(n => n.Span.End < site.SpanStart))
        {
            var assignment = Binding(node);
            if (assignment.Value is null || !Visible(node, site)) continue;
            var next = Origin(assignment.Value, owners);
            var previous = Origin(SyntaxFactory.IdentifierName(assignment.Name), owners);
            owners[assignment.Name] = Controlled(node) ? previous.Join(next) : next;
        }
        return Origin(expression, owners);
    }

    private static (string Name, ExpressionSyntax? Value) Binding(SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax { Initializer: { } initializer } declaration =>
            (declaration.Identifier.Text, initializer.Value),
        AssignmentExpressionSyntax { Left: IdentifierNameSyntax target } value when
            value.IsKind(SyntaxKind.SimpleAssignmentExpression) => (target.Identifier.Text, value.Right),
        _ => ("", null),
    };

    private StorageOrigin Origin(ExpressionSyntax expression, Dictionary<string, StorageOrigin> owners) =>
        Resolver(owners).Origin(expression);

    private StorageOriginResolver Resolver(Dictionary<string, StorageOrigin> owners)
    {
        StorageOrigin? InvocationOrigin(InvocationExpressionSyntax invocation) =>
            callableOwner is null || callableOwners is null
                ? null : ReturnedStorageOriginResolver.Origin(callableOwner, callableOwners, invocation,
                    expression => Origin(expression, owners));
        return new StorageOriginResolver(parameterName, owners, InvocationOrigin);
    }

    private static bool IsAssignmentTarget(ExpressionSyntax expression) =>
        expression.Parent is AssignmentExpressionSyntax assignment && assignment.Left == expression;

    private static bool Controlled(SyntaxNode node) => node.Ancestors().Any(parent => parent is
        IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax
        or DoStatementSyntax or SwitchStatementSyntax or TryStatementSyntax);

    private static bool Visible(SyntaxNode node, SyntaxNode site) =>
        node is not VariableDeclaratorSyntax || node.Ancestors().OfType<BlockSyntax>().FirstOrDefault() is not { } block
        || block.Span.Contains(site.Span);
}
