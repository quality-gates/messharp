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

    private StorageOrigin? Named(ExpressionSyntax expression) => Local(expression) ?? OwnMember(expression)
        ?? (context.Root(expression) is { } root ? state.Heap.Shared(root) : null);

    /// <summary>The origin a local holds, read through its <c>ref</c> location when it has one.</summary>
    private StorageOrigin? Local(ExpressionSyntax expression)
    {
        if (expression is not IdentifierNameSyntax name) return null;
        var local = RefAliasCache.For(context.Body).Canonical(name.Identifier.Text);
        if (state.References.TryGetValue(local, out var location)) return location.Read(state.Heap);
        return state.Bindings.TryGetValue(local, out var bound) ? bound : null;
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
        ConditionalExpressionSyntax choice => Choice(choice),
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

    private StorageOrigin Choice(ConditionalExpressionSyntax choice) => LiteralControlFlow.Boolean(choice.Condition) switch
    {
        true => Origin(choice.WhenTrue),
        false => Origin(choice.WhenFalse),
        _ => Origin(choice.WhenTrue).Join(Origin(choice.WhenFalse)),
    };
}
