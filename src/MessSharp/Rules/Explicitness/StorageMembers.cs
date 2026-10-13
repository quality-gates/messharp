using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class StorageMembers
{
    public static string? OwnField(StorageContext context, ExpressionSyntax expression)
    {
        var name = OwnName(expression);
        if (context.Owner?.Method.Class is not { } cls || name is null) return null;
        if (Shadowed(context.Owner, expression, name)) return null;
        return cls.Fields.Any(f => !f.IsStatic && f.Name == name) ? "." + name : null;
    }

    private static string? OwnName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax bare => bare.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax } member => member.Name.Identifier.Text,
        _ => null,
    };

    private static bool Shadowed(CallableOwner owner, ExpressionSyntax expression, string name) =>
        expression is IdentifierNameSyntax identifier && (owner.Method.Parameters.Any(p => p.Name == name)
            || LocalShadowing.IsShadowedByLocal(identifier, name));

    public static string Index(BracketedArgumentListSyntax arguments) =>
        arguments.Arguments.Count == 1 && arguments.Arguments[0].Expression is LiteralExpressionSyntax literal
            && literal.Token.Value is int index ? "[" + index + "]" : StorageCell.AnyElement;

    public static bool IsField(StorageContext context, MemberAccessExpressionSyntax member)
    {
        if (context.Owner is not { } owner) return false;
        var type = new ExpressionTypes(owner).TypeOf(member.Expression);
        var cls = owner.Method.File.Classes.FirstOrDefault(c => c.Name == type?.Split('<')[0].Split('.').Last());
        return cls?.Fields.Any(f => !f.IsStatic && f.Name == member.Name.Identifier.Text) == true;
    }
}
