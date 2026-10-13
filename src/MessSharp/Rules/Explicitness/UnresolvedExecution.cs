using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class UnresolvedExecution
{
    public static void Add(CallableOwner owner, EffectSummary summary)
    {
        if (owner.Body is null) return;
        foreach (var node in BodyWalk.Nodes(owner.Body))
            if (Operation(owner, node) is { } operation)
                summary.Add(new EffectFact(EffectKind.Unknown, "unresolved " + operation), node);
    }

    private static string? Operation(CallableOwner owner, SyntaxNode node) => node switch
    {
        IdentifierNameSyntax name when PropertyDispatch.IsBare(owner, name) => "property access '" + name + "'",
        MemberAccessExpressionSyntax member when PropertyDispatch.IsMember(owner, member) => "property access '" + member + "'",
        ElementAccessExpressionSyntax element when PropertyDispatch.IsIndexer(owner, element.Expression) => "indexer dispatch",
        BinaryExpressionSyntax binary when Overloaded(owner, binary.OperatorToken.Text, binary.Left, binary.Right) => "operator dispatch",
        PrefixUnaryExpressionSyntax prefix when Overloaded(owner, prefix.OperatorToken.Text, prefix.Operand) => "operator dispatch",
        PostfixUnaryExpressionSyntax postfix when Overloaded(owner, postfix.OperatorToken.Text, postfix.Operand) => "operator dispatch",
        _ => null,
    };

    private static bool Overloaded(CallableOwner owner, string token, params ExpressionSyntax[] operands)
    {
        var types = operands.Select(new ExpressionTypes(owner).TypeOf).OfType<string>().Select(TypeName).ToHashSet();
        return owner.Method.File.Classes.Where(c => types.Contains(c.Name))
            .SelectMany(c => c.Node.DescendantNodes().OfType<OperatorDeclarationSyntax>())
            .Any(operation => operation.OperatorToken.Text == token);
    }

    private static string TypeName(string type) => type.Split('<')[0].Split('.').Last();
}
