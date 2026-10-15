using MessSharp.Model;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ExpressionTypes(CallableOwner owner, IReadOnlyList<CallableOwner>? callableOwners = null)
{
    public string? TypeOf(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax name => Named(name),
        ElementAccessExpressionSyntax element => Element(TypeOf(element.Expression)),
        ParenthesizedExpressionSyntax grouped => TypeOf(grouped.Expression),
        CastExpressionSyntax cast => cast.Type.ToString(),
        MemberAccessExpressionSyntax member => new ExpressionFieldTypes(owner, TypeOf).Find(member),
        InvocationExpressionSyntax invocation => InvocationResult(invocation),
        _ => ExpressionCreationTypes.Find(expression, Agreed, new ExpressionTargetTypes(owner, TypeOf).Find)
            ?? CompositeType(expression),
    };

    private string? CompositeType(ExpressionSyntax expression) => expression switch
    {
        RefExpressionSyntax reference => TypeOf(reference.Expression),
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
            return DeclaredType(local, declaration.Type);
        if (DesignationTypes.Find(name) is { } designated) return designated;
        return owner.Method.Parameters.FirstOrDefault(p => p.Name == name.Identifier.Text)?.Type
            ?? owner.Method.Class?.MemberType(name.Identifier.Text);
    }

    private string? DeclaredType(VariableDeclaratorSyntax local, TypeSyntax type)
    {
        if (type is RefTypeSyntax reference) type = reference.Type;
        return type.ToString() == "var" ? InitializerType(local) : type.ToString();
    }

    internal static VariableDeclaratorSyntax? Binding(IdentifierNameSyntax name) => name.Ancestors()
        .Where(n => n is BlockSyntax or CompilationUnitSyntax)
        .SelectMany(scope => DeclaratorScopes.Named(scope, name.Identifier.Text))
        .FirstOrDefault(v => v.SpanStart < name.SpanStart
            && v.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()
                == name.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault());

    private string? InitializerType(VariableDeclaratorSyntax local) => local.Initializer is { } initializer
        ? TypeOf(initializer.Value) : null;

    private string? InvocationResult(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax member && Result(member) is { } known)
            return known;
        var owners = callableOwners ?? CallableOwnerCache.For(owner.Method.File);
        if (new CallResolver(owners).Resolve(owner, invocation) is { } target) return target.Method.ReturnType;
        return DelegateCallee.Value(invocation) is { } callee ? DelegateParameterTypes.Result(TypeOf(callee)) : null;
    }

    private string? Result(MemberAccessExpressionSyntax member) => member.Name.Identifier.Text switch
    {
        "ToList" => "List<" + Element(TypeOf(member.Expression)) + ">",
        "ToArray" => Element(TypeOf(member.Expression)) + "[]",
        "Select" or "Where" => "IEnumerable<" + Element(TypeOf(member.Expression)) + ">",
        _ => null,
    };

    internal static string? Element(string? type)
    {
        if (type is null) return null;
        if (type.EndsWith(']')) return type[..type.LastIndexOf('[')];
        var start = type.IndexOf('<');
        return start < 0 ? null : type[(start + 1)..type.LastIndexOf('>')];
    }
}
