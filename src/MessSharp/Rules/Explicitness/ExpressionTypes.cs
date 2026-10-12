using MessSharp.Model;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ExpressionTypes(CallableOwner owner)
{
    public string? TypeOf(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax name => Named(name),
        ObjectCreationExpressionSyntax creation => creation.Type.ToString(),
        ImplicitObjectCreationExpressionSyntax
        {
            Parent: EqualsValueClauseSyntax
            {
                Parent: VariableDeclaratorSyntax
                { Parent: VariableDeclarationSyntax declaration }
            }
        } => declaration.Type.ToString(),
        ElementAccessExpressionSyntax element => Element(TypeOf(element.Expression)),
        ParenthesizedExpressionSyntax grouped => TypeOf(grouped.Expression),
        CastExpressionSyntax cast => cast.Type.ToString(),
        MemberAccessExpressionSyntax member when member.Expression is ThisExpressionSyntax =>
            owner.Method.Class?.Fields.FirstOrDefault(f => f.Name == member.Name.Identifier.Text)?.Type,
        InvocationExpressionSyntax invocation => InvocationResult(invocation),
        _ => null,
    };

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
        _ => null,
    };

    private static string? Element(string? type)
    {
        if (type is null) return null;
        if (type.EndsWith("[]", StringComparison.Ordinal)) return type[..^2];
        var start = type.IndexOf('<');
        return start < 0 ? null : type[(start + 1)..type.LastIndexOf('>')];
    }
}
