using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal enum ValueShape { Unknown, Boolean, Value }

/// <summary>
/// Whether an expression computes a built-in boolean or another built-in value rather than a delegate,
/// judged from literals, built-in operators over known operands, catalogued calls and known types.
/// </summary>
internal sealed class ValueShapes(CallableOwner owner, Func<ExpressionSyntax, string?> typeOf)
{
    public ValueShape For(ExpressionSyntax expression) => Ungrouped(expression) switch
    {
        LiteralExpressionSyntax literal => ScalarValueShapes.Literal(literal),
        InterpolatedStringExpressionSyntax => ValueShape.Value,
        PrefixUnaryExpressionSyntax prefix => Prefix(prefix),
        BinaryExpressionSyntax binary => new BinaryValueShapes(this).For(binary),
        ConditionalExpressionSyntax choice => Joined(For(choice.WhenTrue), For(choice.WhenFalse)),
        InvocationExpressionSyntax call => Called(call),
        MemberAccessExpressionSyntax member when IsAmbientInput(member) => Ambient(member),
        var other => ScalarValueShapes.Typed(typeOf(other)),
    };

    public bool IsString(ExpressionSyntax expression) => Ungrouped(expression) switch
    {
        LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.StringLiteralExpression),
        InterpolatedStringExpressionSyntax => true,
        var other => typeOf(other) is "string" or "String" or "System.String",
    };

    private static ExpressionSyntax Ungrouped(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax grouped ? Ungrouped(grouped.Expression) : expression;

    public static ValueShape Joined(ValueShape left, ValueShape right) => left == right ? left : ValueShape.Unknown;

    private ValueShape Prefix(PrefixUnaryExpressionSyntax prefix)
    {
        var operand = For(prefix.Operand);
        if (prefix.IsKind(SyntaxKind.LogicalNotExpression)) return operand == ValueShape.Boolean ? operand : ValueShape.Unknown;
        return operand == ValueShape.Value ? operand : ValueShape.Unknown;
    }

    private ValueShape Called(InvocationExpressionSyntax call)
    {
        if (KnownParseOperations.IsKnown(call)) return ScalarValueShapes.Parsed(call);
        if (call.Expression is not MemberAccessExpressionSyntax member) return ScalarValueShapes.Typed(typeOf(call));
        if (IsAmbientInput(member)) return Ambient(member);
        var name = member.Name.Identifier.Text;
        if (KnownMathOperations.Matches(owner, member.Expression, name)) return ValueShape.Value;
        if (typeOf(member.Expression) is { } receiver && KnownQueries.IsStringCalculation(receiver, name))
            return KnownQueries.IsStringPredicate(name) ? ValueShape.Boolean : ValueShape.Value;
        return ScalarValueShapes.Typed(typeOf(call));
    }

    private bool IsAmbientInput(MemberAccessExpressionSyntax member) =>
        AmbientApi.Inputs.UseAt(member) is not null && KnownTypeChecks.StandardReceiver(owner, member.Expression);

    private static ValueShape Ambient(MemberAccessExpressionSyntax member) =>
        AmbientApi.Predicates.UseAt(member) is not null ? ValueShape.Boolean : ValueShape.Value;
}

internal static class ScalarValueShapes
{
    public static ValueShape Literal(LiteralExpressionSyntax literal) => literal.Kind() switch
    {
        SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression => ValueShape.Boolean,
        SyntaxKind.NumericLiteralExpression or SyntaxKind.StringLiteralExpression
            or SyntaxKind.CharacterLiteralExpression => ValueShape.Value,
        _ => ValueShape.Unknown,
    };

    public static ValueShape Parsed(InvocationExpressionSyntax call) =>
        call.Expression is MemberAccessExpressionSyntax { Expression: PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.BoolKeyword } }
            ? ValueShape.Boolean : ValueShape.Value;

    public static ValueShape Typed(string? type)
    {
        var keyword = type is null ? SyntaxKind.None : SyntaxFacts.GetKeywordKind(type);
        if (keyword == SyntaxKind.BoolKeyword) return ValueShape.Boolean;
        return SyntaxFacts.IsPredefinedType(keyword) && keyword != SyntaxKind.VoidKeyword ? ValueShape.Value : ValueShape.Unknown;
    }
}

/// <summary>Built-in operators: comparisons and logic give booleans, arithmetic and concatenation give values.</summary>
internal sealed class BinaryValueShapes(ValueShapes shapes)
{
    public ValueShape For(BinaryExpressionSyntax binary)
    {
        if (IsStringConcatenation(binary)) return ValueShape.Value;
        var left = shapes.For(binary.Left);
        var right = shapes.For(binary.Right);
        return binary.Kind() switch
        {
            SyntaxKind.IsExpression => ValueShape.Boolean,
            var kind when IsLogical(kind) => Both(left, right, ValueShape.Boolean),
            var kind when IsBitwise(kind) => ValueShapes.Joined(left, right),
            var kind when IsComparison(kind) => Compared(left, right),
            var kind when IsArithmetic(kind) => Both(left, right, ValueShape.Value),
            _ => ValueShape.Unknown,
        };
    }

    private bool IsStringConcatenation(BinaryExpressionSyntax binary) =>
        binary.IsKind(SyntaxKind.AddExpression) && (shapes.IsString(binary.Left) || shapes.IsString(binary.Right));

    private static bool IsLogical(SyntaxKind kind) =>
        kind is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression;

    private static bool IsBitwise(SyntaxKind kind) =>
        kind is SyntaxKind.BitwiseAndExpression or SyntaxKind.BitwiseOrExpression or SyntaxKind.ExclusiveOrExpression;

    private static ValueShape Both(ValueShape left, ValueShape right, ValueShape shape) =>
        left == shape && right == shape ? shape : ValueShape.Unknown;

    private static ValueShape Compared(ValueShape left, ValueShape right) =>
        left != ValueShape.Unknown && right != ValueShape.Unknown ? ValueShape.Boolean : ValueShape.Unknown;

    private static bool IsComparison(SyntaxKind kind) =>
        kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression or SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression;

    private static bool IsArithmetic(SyntaxKind kind) =>
        kind is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression
            or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression or SyntaxKind.LeftShiftExpression
            or SyntaxKind.RightShiftExpression or SyntaxKind.UnsignedRightShiftExpression;
}
