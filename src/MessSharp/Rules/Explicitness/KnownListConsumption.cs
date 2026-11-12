using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class KnownListConsumption
{
    private static readonly IReadOnlySet<SyntaxKind> UnaryOperators = new HashSet<SyntaxKind>
    {
        SyntaxKind.UnaryPlusExpression, SyntaxKind.UnaryMinusExpression, SyntaxKind.BitwiseNotExpression,
    };

    private static readonly IReadOnlySet<SyntaxKind> BinaryOperators = new HashSet<SyntaxKind>
    {
        SyntaxKind.AddExpression, SyntaxKind.SubtractExpression, SyntaxKind.MultiplyExpression,
        SyntaxKind.DivideExpression, SyntaxKind.ModuloExpression, SyntaxKind.BitwiseAndExpression,
        SyntaxKind.BitwiseOrExpression, SyntaxKind.ExclusiveOrExpression, SyntaxKind.LeftShiftExpression,
        SyntaxKind.RightShiftExpression, SyntaxKind.UnsignedRightShiftExpression,
    };

    public static ExpressionSyntax? ConstructorArgument(CallableOwner owner, BaseObjectCreationExpressionSyntax creation)
    {
        var type = new ExpressionTypes(owner).TypeOf(creation);
        if (type is null || !IsStandardList(owner, SequenceTypeAliases.Resolve(owner, type))) return null;
        if (creation.ArgumentList?.Arguments is not [var argument] || argument.NameColon?.Name.Identifier.Text == "capacity") return null;
        return CouldBeSequence(owner, argument.Expression) ? argument.Expression : null;
    }

    public static ExpressionSyntax? AddRangeArgument(CallableOwner owner, InvocationExpressionSyntax call)
    {
        if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "AddRange" } member
            || call.ArgumentList.Arguments is not [var argument]) return null;
        var type = new ExpressionTypes(owner).TypeOf(member.Expression);
        if (type is null || !IsStandardList(owner, SequenceTypeAliases.Resolve(owner, type))) return null;
        return CouldBeSequence(owner, argument.Expression) ? argument.Expression : null;
    }

    internal static bool IsStandardList(CallableOwner owner, string type)
    {
        var name = type.Split('<')[0];
        return name is "System.Collections.Generic.List" or "global::System.Collections.Generic.List"
            || name == "List" && !KnownTypeChecks.IsCustomType(owner, type);
    }

    private static bool CouldBeSequence(CallableOwner owner, ExpressionSyntax value)
    {
        if (KnownExecutionOperations.IsSequence(owner, value)) return true;
        if (value is LiteralExpressionSyntax) return false;
        var type = new ExpressionTypes(owner).TypeOf(value);
        return type is not ("int" or "System.Int32" or "string" or "System.String") && !IsNumericExpression(owner, value);
    }

    private static bool IsNumericExpression(CallableOwner owner, ExpressionSyntax expression)
    {
        if (new ExpressionTypes(owner).TypeOf(expression) is "int" or "System.Int32") return true;
        return expression switch
        {
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.NumericLiteralExpression),
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } =>
                KnownMathOperations.Matches(owner, member.Expression, member.Name.Identifier.Text),
            ParenthesizedExpressionSyntax parenthesized => IsNumericExpression(owner, parenthesized.Expression),
            PrefixUnaryExpressionSyntax prefix when UnaryOperators.Contains(prefix.Kind()) => IsNumericExpression(owner, prefix.Operand),
            BinaryExpressionSyntax binary when BinaryOperators.Contains(binary.Kind()) =>
                IsNumericExpression(owner, binary.Left) && IsNumericExpression(owner, binary.Right),
            _ => false,
        };
    }
}
