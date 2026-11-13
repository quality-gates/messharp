using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class KnownCollectionContains
{
    public static ExpressionSyntax? Receiver(CallableOwner owner, InvocationExpressionSyntax call)
    {
        if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "Contains" } member
            || call.ArgumentList.Arguments is not [var argument]) return null;
        var types = new ExpressionTypes(owner);
        var type = types.TypeOf(member.Expression);
        if (type is null) return null;
        type = SequenceTypeAliases.Resolve(owner, type);
        if (!IsStandardCollection(owner, type) || !MatchesElement(types, type, argument.Expression)) return null;
        return member.Expression;
    }

    private static bool IsStandardCollection(CallableOwner owner, string type)
    {
        if (KnownListConsumption.IsStandardList(owner, type)) return true;
        var name = type.Split('<')[0];
        return name is "System.Collections.Generic.HashSet" or "global::System.Collections.Generic.HashSet"
            || name == "HashSet" && !KnownTypeChecks.IsCustomType(owner, type);
    }

    private static bool MatchesElement(ExpressionTypes types, string type, ExpressionSyntax argument)
    {
        var element = ExpressionTypes.Element(type);
        if (element is null || !LiteralArguments.Accepts(element, argument)) return false;
        var argumentType = types.TypeOf(argument);
        return argumentType is not ("string" or "System.String") || element is "string" or "System.String" or "object" or "System.Object";
    }
}
