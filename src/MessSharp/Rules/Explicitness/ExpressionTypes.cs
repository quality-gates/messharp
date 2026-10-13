using MessSharp.Model;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ExpressionTypes(CallableOwner owner)
{
    public string? TypeOf(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax name => Named(name),
        ElementAccessExpressionSyntax element => Element(TypeOf(element.Expression)),
        ParenthesizedExpressionSyntax grouped => TypeOf(grouped.Expression),
        CastExpressionSyntax cast => cast.Type.ToString(),
        MemberAccessExpressionSyntax member when member.Expression is ThisExpressionSyntax =>
            owner.Method.Class?.Fields.FirstOrDefault(f => f.Name == member.Name.Identifier.Text)?.Type,
        MemberAccessExpressionSyntax member => FieldType(member),
        InvocationExpressionSyntax invocation => InvocationResult(invocation),
        _ => CreationType(expression) ?? CompositeType(expression),
    };

    private string? CreationType(ExpressionSyntax expression) => expression switch
    {
        ObjectCreationExpressionSyntax creation => creation.Type.ToString(),
        ArrayCreationExpressionSyntax array => array.Type.ElementType + string.Concat(array.Type.RankSpecifiers
            .Select(rank => "[" + new string(',', rank.Rank - 1) + "]")),
        ImplicitArrayCreationExpressionSyntax array => Agreed(array.Initializer.Expressions) is { } element ? element + "[]" : null,
        ImplicitObjectCreationExpressionSyntax
        {
            Parent: EqualsValueClauseSyntax
            {
                Parent: VariableDeclaratorSyntax
                { Parent: VariableDeclarationSyntax declaration }
            }
        } => declaration.Type.ToString(),
        _ => null,
    };

    private string? CompositeType(ExpressionSyntax expression) => expression switch
    {
        ConditionalExpressionSyntax choice => Agreed([choice.WhenTrue, choice.WhenFalse]),
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce =>
            Agreed([coalesce.Left, coalesce.Right]),
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression, Right: TypeSyntax type } => type.ToString(),
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } forgiven =>
            TypeOf(forgiven.Operand),
        SwitchExpressionSyntax arms => Agreed(arms.Arms.Select(arm => arm.Expression)),
        _ => null,
    };

    private string? Agreed(IEnumerable<ExpressionSyntax> branches)
    {
        var types = branches.Where(b => b is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression })
            .Select(TypeOf).Distinct(StringComparer.Ordinal).Take(2).ToList();
        return types.Count == 1 ? types[0] : null;
    }

    private string? Named(IdentifierNameSyntax name)
    {
        var local = Binding(name);
        if (local?.Parent is VariableDeclarationSyntax declaration)
            return declaration.Type.ToString() == "var" ? InitializerType(local) : declaration.Type.ToString();
        return owner.Method.Parameters.FirstOrDefault(p => p.Name == name.Identifier.Text)?.Type
            ?? owner.Method.Class?.Fields.FirstOrDefault(f => f.Name == name.Identifier.Text)?.Type;
    }

    internal static VariableDeclaratorSyntax? Binding(IdentifierNameSyntax name) => name.Ancestors()
        .Where(n => n is BlockSyntax or CompilationUnitSyntax)
        .SelectMany(scope => scope.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(v => v.Ancestors().FirstOrDefault(n => n is BlockSyntax or CompilationUnitSyntax) == scope))
        .FirstOrDefault(v => v.Identifier.Text == name.Identifier.Text && v.SpanStart < name.SpanStart);

    private string? InitializerType(VariableDeclaratorSyntax local) => local.Initializer is { } initializer
        ? TypeOf(initializer.Value) : null;

    private string? FieldType(MemberAccessExpressionSyntax member)
    {
        var type = TypeOf(member.Expression)?.Split('<')[0].Split('.').Last();
        return owner.Method.File.Classes.FirstOrDefault(c => c.Name == type)?.Fields
            .FirstOrDefault(f => f.Name == member.Name.Identifier.Text)?.Type;
    }

    private string? InvocationResult(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax member && Result(member) is { } known)
            return known;
        var owners = CallableOwner.Gather(owner.Method.File);
        return new CallResolver(owners).Resolve(owner, invocation)?.Method.ReturnType;
    }

    private string? Result(MemberAccessExpressionSyntax member) => member.Name.Identifier.Text switch
    {
        "ToList" => "List<" + Element(TypeOf(member.Expression)) + ">",
        "ToArray" => Element(TypeOf(member.Expression)) + "[]",
        "Select" or "Where" => "IEnumerable<" + Element(TypeOf(member.Expression)) + ">",
        _ => null,
    };

    private static string? Element(string? type)
    {
        if (type is null) return null;
        if (type.EndsWith(']')) return type[..type.LastIndexOf('[')];
        var start = type.IndexOf('<');
        return start < 0 ? null : type[(start + 1)..type.LastIndexOf('>')];
    }
}
