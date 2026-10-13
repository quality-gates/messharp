using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageOriginResolver(StorageContext context, StorageEnvironment state,
    Func<ExpressionSyntax, StorageOrigin?> fresh, Func<InvocationExpressionSyntax, StorageOrigin?> invocation)
{
    public StorageOrigin Origin(ExpressionSyntax expression)
    {
        expression = CallableSyntax.Unwrap(expression);
        if (Named(expression) is { } named) return named;
        if (Access(expression) is { } access) return access;
        if (Composite(expression) is { } composite) return composite;
        if (expression is InvocationExpressionSyntax call && invocation(call) is { } called) return called;
        return fresh(expression) ?? StorageOrigin.Private;
    }

    private StorageOrigin? Named(ExpressionSyntax expression)
    {
        if (expression is IdentifierNameSyntax name && state.Bindings.TryGetValue(name.Identifier.Text, out var bound)) return bound;
        if (OwnMember(expression) is { } own) return own;
        return context.Root(expression) is { } root ? state.Heap.Shared(root) : null;
    }

    private StorageOrigin? OwnMember(ExpressionSyntax expression)
    {
        if (!state.Bindings.TryGetValue(StorageContext.ThisBinding, out var receiver)) return null;
        if (expression is ThisExpressionSyntax or BaseExpressionSyntax) return receiver;
        return StorageMembers.OwnField(context, expression) is { } slot ? state.Heap.Read(receiver, slot) : null;
    }

    private StorageOrigin? Access(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => state.Heap.Read(Origin(member.Expression), "." + member.Name.Identifier.Text),
        ElementAccessExpressionSyntax element => state.Heap.Read(Origin(element.Expression), StorageMembers.Index(element.ArgumentList)),
        _ => null,
    };

    private StorageOrigin? Composite(ExpressionSyntax expression) => expression switch
    {
        ConditionalExpressionSyntax choice => Origin(choice.WhenTrue).Join(Origin(choice.WhenFalse)),
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce =>
            Origin(coalesce.Left).Join(Origin(coalesce.Right)),
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } converted =>
            Origin(converted.Left),
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } forgiven =>
            Origin(forgiven.Operand),
        SwitchExpressionSyntax choice => choice.Arms.Select(arm => Origin(arm.Expression))
            .Aggregate(StorageOrigin.Private, (current, next) => current.Join(next)),
        _ => null,
    };
}
