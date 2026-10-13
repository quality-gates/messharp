using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ExpressionFieldTypes(CallableOwner owner, Func<ExpressionSyntax, string?> typeOf)
{
    public string? Find(MemberAccessExpressionSyntax member)
    {
        if (OwnReceiver(member.Expression))
            return owner.Method.Class?.Fields.FirstOrDefault(f => f.Name == member.Name.Identifier.Text)?.Type;
        var type = typeOf(member.Expression)?.Split('<')[0].Split('.').Last();
        return owner.Method.File.Classes.FirstOrDefault(c => c.Name == type)?.Fields
            .FirstOrDefault(f => f.Name == member.Name.Identifier.Text)?.Type;
    }

    private bool OwnReceiver(ExpressionSyntax receiver) => receiver is ThisExpressionSyntax
        || receiver is IdentifierNameSyntax qualifier && qualifier.Identifier.Text == owner.Method.Class?.Name;
}
