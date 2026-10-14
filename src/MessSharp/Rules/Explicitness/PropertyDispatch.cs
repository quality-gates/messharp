using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class PropertyDispatch
{
    private static readonly IReadOnlySet<string> TrustedTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "string", "String", "IList", "IReadOnlyList", "ICollection", "IEnumerable",
        "DateTime", "DateTimeOffset", "TimeSpan", "Guid",
    };

    public static bool IsBare(CallableOwner owner, IdentifierNameSyntax name)
    {
        if (name.Parent is MemberAccessExpressionSyntax or MemberBindingExpressionSyntax or TypeSyntax || owner.Method.Class is not { } cls) return false;
        if (owner.Method.Parameters.Any(parameter => parameter.Name == name.Identifier.Text)
            || LocalShadowing.IsShadowedByLocal(name, name.Identifier.Text)) return false;
        return cls.Node.DescendantNodes().OfType<PropertyDeclarationSyntax>().Any(property => property.Identifier.Text == name.Identifier.Text);
    }

    public static bool IsMember(CallableOwner owner, MemberAccessExpressionSyntax member)
    {
        if (member.Parent is InvocationExpressionSyntax call && call.Expression == member) return false;
        if (AmbientChain(owner, member)) return false;
        if (member.Expression is ThisExpressionSyntax)
            return owner.Method.Class?.Node.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .Any(property => property.Identifier.Text == member.Name.Identifier.Text) == true;
        var type = new ExpressionTypes(owner).TypeOf(member.Expression);
        if (type is null) return DeclaredProperty(owner, member) || MayForceLazy(member);
        if (KnownExecutionOperations.IsLazyValue(owner, member.Expression) && member.Name.Identifier.Text == "Value") return false;
        return RequiresDispatch(owner, member.Name.Identifier.Text, type);
    }

    public static bool IsConditional(CallableOwner owner, MemberBindingExpressionSyntax member)
    {
        if (member.Parent is InvocationExpressionSyntax) return false;
        var receiver = member.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression;
        if (receiver is null) return true;
        if (KnownExecutionOperations.IsLazyValue(owner, receiver) && member.Name.Identifier.Text == "Value") return false;
        var type = new ExpressionTypes(owner).TypeOf(receiver);
        return type is null || RequiresDispatch(owner, member.Name.Identifier.Text, type);
    }

    public static bool IsIndexer(CallableOwner owner, ExpressionSyntax receiver)
    {
        var type = new ExpressionTypes(owner).TypeOf(receiver);
        return type is null || !TrustedValue(type) || KnownTypeChecks.IsCustomType(owner, type);
    }

    private static bool RequiresDispatch(CallableOwner owner, string name, string type)
    {
        if (TrustedValue(type) && !KnownTypeChecks.IsCustomType(owner, type)) return false;
        var cls = owner.Method.File.Classes.FirstOrDefault(candidate => candidate.Name == TypeName(type));
        return cls is null || cls.Node.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .Any(property => property.Identifier.Text == name);
    }

    /// <summary>An untyped receiver may be a Lazy, and reading its Value runs an unknown factory.</summary>
    private static bool MayForceLazy(MemberAccessExpressionSyntax member) => member.Name.Identifier.Text == "Value";

    private static bool DeclaredProperty(CallableOwner owner, MemberAccessExpressionSyntax member) => owner.Method.File.Classes
        .Where(candidate => candidate.Name == member.Expression.ToString())
        .SelectMany(candidate => candidate.Node.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        .Any(property => property.Identifier.Text == member.Name.Identifier.Text);

    private static bool AmbientChain(CallableOwner owner, MemberAccessExpressionSyntax member) =>
        member.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>().Any(access =>
            (AmbientApi.Inputs.UseAt(access) is not null || AmbientApi.Outputs.UseAt(access) is not null)
            && KnownTypeChecks.StandardReceiver(owner, access.Expression));

    private static bool TrustedValue(string type) => StorageTypes.IsMutable(type) || TrustedTypes.Contains(TypeName(type));

    private static string TypeName(string type) => type.Split('<')[0].Split('.').Last();
}
