using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class OperatorDispatch
{
    public static bool RequiresExecution(CallableOwner owner, SyntaxNode node) => node switch
    {
        BinaryExpressionSyntax binary => Overloaded(owner, binary.OperatorToken.Text, binary.Left, binary.Right),
        PrefixUnaryExpressionSyntax prefix => Overloaded(owner, prefix.OperatorToken.Text, prefix.Operand),
        PostfixUnaryExpressionSyntax postfix => Overloaded(owner, postfix.OperatorToken.Text, postfix.Operand),
        AssignmentExpressionSyntax assignment when assignment.OperatorToken.Text.Length > 1 =>
            Overloaded(owner, assignment.OperatorToken.Text[..^1], assignment.Left, assignment.Right),
        CastExpressionSyntax cast => Converted(owner, cast),
        _ => false,
    };

    private static bool Overloaded(CallableOwner owner, string token, params ExpressionSyntax[] operands)
    {
        var types = operands.Select(new ExpressionTypes(owner).TypeOf).OfType<string>().Select(TypeName).ToHashSet();
        return owner.Method.File.Classes.Where(c => types.Contains(c.Name))
            .SelectMany(c => c.Node.DescendantNodes().OfType<OperatorDeclarationSyntax>())
            .Any(operation => operation.OperatorToken.Text == token);
    }

    private static bool Converted(CallableOwner owner, CastExpressionSyntax cast)
    {
        var source = new ExpressionTypes(owner).TypeOf(cast.Expression);
        var destination = cast.Type.ToString();
        if (source is null || TypeName(source) == TypeName(destination)) return false;
        var types = new[] { source, destination }.Select(TypeName).ToHashSet();
        return owner.Method.File.Classes.Where(c => types.Contains(c.Name))
            .SelectMany(c => c.Node.DescendantNodes().OfType<ConversionOperatorDeclarationSyntax>())
            .Any(operation => TypeName(operation.Type.ToString()) == TypeName(destination)
                && operation.ParameterList.Parameters.Count == 1
                && TypeName(operation.ParameterList.Parameters[0].Type?.ToString() ?? "") == TypeName(source));
    }

    private static string TypeName(string type) => type.Split('<')[0].Split('.').Last();
}
