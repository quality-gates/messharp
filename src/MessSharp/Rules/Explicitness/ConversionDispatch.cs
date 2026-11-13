using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// User-defined conversions and <c>operator true</c>/<c>operator false</c> that a plain-looking expression runs:
/// an initializer, assignment or return that converts, and a condition or short-circuit that tests a value.
/// </summary>
internal static class ConversionDispatch
{
    public static bool RequiresExecution(CallableOwner owner, SyntaxNode node) => node switch
    {
        VariableDeclaratorSyntax { Initializer.Value: { } value, Parent: VariableDeclarationSyntax { Type: { IsVar: false } type } } =>
            Converts(owner, value, type.ToString()),
        AssignmentExpressionSyntax assignment when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) =>
            new ExpressionTypes(owner).TypeOf(assignment.Left) is { } destination && Converts(owner, assignment.Right, destination),
        ReturnStatementSyntax { Expression: { } value } => owner.Method.ReturnType is { Length: > 0 } returned && Converts(owner, value, returned),
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression) =>
            Tests(owner, binary.Left),
        _ => Condition(node) is { } condition && Tests(owner, condition),
    };

    /// <summary>Whether a value of <paramref name="source"/>'s type converts to <paramref name="destination"/> through a user-defined operator.</summary>
    public static bool Converts(CallableOwner owner, ExpressionSyntax source, string destination) =>
        new ExpressionTypes(owner).TypeOf(source) is { } type && Converts(owner, type, destination);

    public static bool Converts(CallableOwner owner, string source, string destination)
    {
        if (TypeName(source) == TypeName(destination)) return false;
        var types = new[] { source, destination }.Select(TypeName).ToHashSet();
        return owner.Method.File.Classes.Where(c => types.Contains(c.Name))
            .SelectMany(c => c.Node.DescendantNodes().OfType<ConversionOperatorDeclarationSyntax>())
            .Any(operation => TypeName(operation.Type.ToString()) == TypeName(destination)
                && operation.ParameterList.Parameters.Count == 1
                && TypeName(operation.ParameterList.Parameters[0].Type?.ToString() ?? "") == TypeName(source));
    }

    private static ExpressionSyntax? Condition(SyntaxNode node) => node switch
    {
        IfStatementSyntax statement => statement.Condition,
        WhileStatementSyntax statement => statement.Condition,
        DoStatementSyntax statement => statement.Condition,
        ForStatementSyntax statement => statement.Condition,
        ConditionalExpressionSyntax conditional => conditional.Condition,
        _ => null,
    };

    /// <summary>Whether testing <paramref name="value"/> as a condition runs a user-defined <c>operator true</c> or <c>false</c>.</summary>
    private static bool Tests(CallableOwner owner, ExpressionSyntax value) =>
        new ExpressionTypes(owner).TypeOf(value) is { } type && owner.Method.File.Classes.Where(c => c.Name == TypeName(type))
            .SelectMany(c => c.Node.DescendantNodes().OfType<OperatorDeclarationSyntax>())
            .Any(operation => operation.OperatorToken.Text is "true" or "false");

    private static string TypeName(string type) => type.Split('<')[0].Split('.').Last();
}
