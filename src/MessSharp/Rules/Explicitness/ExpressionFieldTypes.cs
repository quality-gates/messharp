using MessSharp.Model;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class ExpressionFieldTypes(CallableOwner owner, Func<ExpressionSyntax, string?> typeOf)
{
    public string? Find(MemberAccessExpressionSyntax member)
    {
        if (OwnReceiver(member.Expression))
            return owner.Method.Class?.MemberType(member.Name.Identifier.Text);
        var type = typeOf(member.Expression)?.Split('<')[0].Split('.').Last();
        return owner.Method.File.Classes.FirstOrDefault(c => c.Name == type)?.MemberType(member.Name.Identifier.Text);
    }

    private bool OwnReceiver(ExpressionSyntax receiver) => receiver is ThisExpressionSyntax
        || receiver is IdentifierNameSyntax qualifier && qualifier.Identifier.Text == owner.Method.Class?.Name;
}

internal static class ClassMemberTypes
{
    /// <summary>The declared type of a field or property of <paramref name="cls"/>, whatever its accessor form.</summary>
    public static string? MemberType(this ClassModel cls, string name) =>
        cls.Fields.FirstOrDefault(field => field.Name == name)?.Type
        ?? cls.Node.ChildNodes().OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(property => property.Identifier.Text == name)?.Type.ToString();
}
