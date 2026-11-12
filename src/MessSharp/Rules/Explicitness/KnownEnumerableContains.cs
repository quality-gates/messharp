using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class KnownEnumerableContains
{
    private static readonly IReadOnlySet<string> Receivers = new HashSet<string>(StringComparer.Ordinal)
    {
        "Enumerable", "System.Linq.Enumerable", "global::System.Linq.Enumerable",
    };

    public static ExpressionSyntax? Source(CallableOwner owner, InvocationExpressionSyntax call)
    {
        if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "Contains" } member) return null;
        var receiver = SequenceTypeAliases.Resolve(owner, member.Expression.ToString());
        if (!Receivers.Contains(receiver)) return null;
        if (receiver == "Enumerable" && !KnownTypeChecks.StandardReceiver(owner, member.Expression)) return null;
        var arguments = call.ArgumentList.Arguments;
        if (arguments.Count is < 2 or > 3) return null;
        return (arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == "source") ?? arguments[0]).Expression;
    }
}
