using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class DelegateParameterTypes(CallableOwner parent, IReadOnlyList<CallableOwner> owners)
{
    public string? ReturnType(SyntaxNode node)
    {
        var expected = Expected(node);
        var generic = expected is null ? null : Generic(SyntaxFactory.ParseTypeName(expected));
        return generic?.Identifier.Text switch
        {
            "Func" => generic.TypeArgumentList.Arguments.LastOrDefault()?.ToString(),
            "Action" => "void",
            "Predicate" => "bool",
            "Comparison" => "int",
            _ => null,
        };
    }

    public IReadOnlyList<string> For(SyntaxNode node)
    {
        var expected = Expected(node);
        if (expected is not null) return Parameters(SyntaxFactory.ParseTypeName(expected));
        if (node.Parent is ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax call }
            && call.Expression is MemberAccessExpressionSyntax member && KnownCollectionCallback(call))
        {
            var element = ExpressionTypes.Element(new ExpressionTypes(parent, owners).TypeOf(member.Expression));
            return element is null ? [] : Enumerable.Repeat(element, CallableOwner.Parameters(node).Count()).ToList();
        }
        return [];
    }

    private string? Expected(SyntaxNode node) => node.Parent switch
    {
        ParenthesizedExpressionSyntax grouped => Expected(grouped),
        CastExpressionSyntax cast => cast.Type.ToString(),
        EqualsValueClauseSyntax { Parent.Parent: VariableDeclarationSyntax declaration } => declaration.Type.ToString(),
        AssignmentExpressionSyntax assignment => new ExpressionTypes(parent, owners).TypeOf(assignment.Left),
        ArgumentSyntax argument => ArgumentType(argument),
        _ => null,
    };

    private string? ArgumentType(ArgumentSyntax argument)
    {
        if (argument.Parent is not ArgumentListSyntax { Parent: InvocationExpressionSyntax call } list
            || new CallResolver(owners).Resolve(parent, call) is not { } target) return null;
        if (argument.NameColon is { } named)
            return target.Method.Parameters.FirstOrDefault(parameter => parameter.Name == named.Name.Identifier.Text)?.Type;
        return target.Method.Parameters.ElementAtOrDefault(list.Arguments.IndexOf(argument))?.Type;
    }

    private static IReadOnlyList<string> Parameters(TypeSyntax type)
    {
        var generic = Generic(type);
        if (generic is null) return [];
        var arguments = generic.TypeArgumentList.Arguments.Select(argument => argument.ToString()).ToList();
        return generic.Identifier.Text switch
        {
            "Action" => arguments,
            "Func" => arguments.SkipLast(1).ToList(),
            "Predicate" => arguments.Take(1).ToList(),
            "Comparison" => arguments.Take(1).Concat(arguments.Take(1)).ToList(),
            _ => [],
        };
    }

    private static GenericNameSyntax? Generic(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic,
        QualifiedNameSyntax qualified => Generic(qualified.Right),
        AliasQualifiedNameSyntax alias => Generic(alias.Name),
        NullableTypeSyntax nullable => Generic(nullable.ElementType),
        _ => null,
    };

    private bool KnownCollectionCallback(InvocationExpressionSyntax call) =>
        KnownExecutionOperations.IsColdQuery(parent, call, owners) || KnownExecutionOperations.IsEagerCallback(parent, call, owners);
}
