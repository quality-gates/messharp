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
        if (Receiver(expression) is { } receiver) return Origin(receiver).Element();
        if (expression is ConditionalExpressionSyntax choice) return ChoiceOrigin(choice);
        return FreshOrigin(expression);
    }

    private StorageOrigin Named(string name) =>
        owners.TryGetValue(name, out var owner) ? owner
            : parameterName(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.IdentifierName(name)) is { } original
                ? StorageOrigin.Shared(original) : StorageOrigin.Private;

    private StorageOrigin ChoiceOrigin(ConditionalExpressionSyntax choice) =>
        Origin(choice.WhenTrue).Join(Origin(choice.WhenFalse));

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
