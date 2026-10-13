using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class StorageOriginResolver(
    Func<ExpressionSyntax, string?> parameterName,
    Dictionary<string, StorageOrigin> owners,
    Func<InvocationExpressionSyntax, StorageOrigin?>? invocationOrigin = null)
{
    public StorageOrigin Origin(ExpressionSyntax expression)
    {
        expression = CallableSyntax.Unwrap(expression);
        if (expression is IdentifierNameSyntax name) return Named(name.Identifier.Text);
        if (parameterName(expression) is { } state) return StorageOrigin.Shared(state);
        if (Receiver(expression) is { } receiver) return Origin(receiver).Element();
        if (CompositeOrigin(expression) is { } composite) return composite;
        return FreshOrigin(expression);
    }

    private StorageOrigin Named(string name) =>
        owners.TryGetValue(name, out var owner) ? owner
            : parameterName(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.IdentifierName(name)) is { } original
                ? StorageOrigin.Shared(original) : StorageOrigin.Private;

    private StorageOrigin? CompositeOrigin(ExpressionSyntax expression) => expression switch
    {
        ConditionalExpressionSyntax choice => Origin(choice.WhenTrue).Join(Origin(choice.WhenFalse)),
        BinaryExpressionSyntax { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.CoalesceExpression } coalesce =>
            Origin(coalesce.Left).Join(Origin(coalesce.Right)),
        BinaryExpressionSyntax { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsExpression } converted =>
            Origin(converted.Left),
        PostfixUnaryExpressionSyntax { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.SuppressNullableWarningExpression } forgiven =>
            Origin(forgiven.Operand),
        SwitchExpressionSyntax choice => choice.Arms.Select(arm => Origin(arm.Expression))
            .Aggregate(StorageOrigin.Private, (current, next) => current.Join(next)),
        CollectionExpressionSyntax collection => CollectionOrigin(collection),
        _ => null,
    };

    private StorageOrigin CollectionOrigin(CollectionExpressionSyntax collection) => collection.Elements
        .Select(element => element switch
        {
            SpreadElementSyntax spread => Origin(spread.Expression).ShallowCopy(),
            ExpressionElementSyntax item => StorageOrigin.Holding(Origin(item.Expression)),
            _ => StorageOrigin.Unknown,
        }).Aggregate(StorageOrigin.Private, (current, next) => current.Join(next));

    private StorageOrigin FreshOrigin(ExpressionSyntax expression)
    {
        if (expression is InvocationExpressionSyntax invocation && invocationOrigin?.Invoke(invocation) is { } origin)
            return origin;
        return expression switch
        {
            ObjectCreationExpressionSyntax creation => CopyOrigin(creation.ArgumentList),
            ImplicitObjectCreationExpressionSyntax creation => CopyOrigin(creation.ArgumentList),
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } when IsShallowCopy(member) =>
                Origin(member.Expression).ShallowCopy(),
            _ => StorageOrigin.Private,
        };
    }

    private StorageOrigin CopyOrigin(ArgumentListSyntax? arguments) =>
        arguments?.Arguments.Count == 1 ? Origin(arguments.Arguments[0].Expression).ShallowCopy() : StorageOrigin.Private;

    private static bool IsShallowCopy(MemberAccessExpressionSyntax member) =>
        member.Name.Identifier.Text is "Clone" or "ToList" or "ToArray";

    private static ExpressionSyntax? Receiver(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Expression,
        ElementAccessExpressionSyntax element => element.Expression,
        _ => null,
    };
}
