using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class KnownOperations
{
    public static bool IsKnown(CallableOwner owner, InvocationExpressionSyntax call)
    {
        if (call.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" }) return true;
        if (KnownParseOperations.IsKnown(call)) return true;
        if (CallParts(call) is not { } parts) return false;
        var (receiver, name, member) = parts;
        return IsKnownMember(owner, receiver, name, member);
    }

    private static bool IsKnownMember(
        CallableOwner owner, ExpressionSyntax receiver, string name, MemberAccessExpressionSyntax? member)
    {
        if (KnownAmbientOperations.Matches(owner, receiver, member)) return true;
        if (KnownMathOperations.Matches(owner, receiver, name)) return true;
        var type = new ExpressionTypes(owner).TypeOf(receiver);
        if (type is null || KnownTypeChecks.IsCustomType(owner, type)) return false;
        if (KnownExecutionOperations.IsSequence(owner, receiver))
            return KnownQueries.IsCollectionQuery(name) || KnownMutations.IsMutation(name) || name is "Select" or "Where";
        return KnownQueries.IsStringCalculation(type, name);
    }

    private static (ExpressionSyntax Receiver, string Name, MemberAccessExpressionSyntax? Member)? CallParts(
        InvocationExpressionSyntax call)
    {
        var member = call.Expression as MemberAccessExpressionSyntax;
        var receiver = member?.Expression ?? (call.Parent as ConditionalAccessExpressionSyntax)?.Expression;
        var name = member?.Name.Identifier.Text ?? (call.Expression as MemberBindingExpressionSyntax)?.Name.Identifier.Text;
        return receiver is null || name is null ? null : (receiver, name, member);
    }

}

internal readonly record struct SyntaxNodeCall(ExpressionSyntax Receiver, string Name);
