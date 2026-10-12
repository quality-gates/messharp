using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class KnownAmbientOperations
{
    public static bool Matches(CallableOwner owner, ExpressionSyntax receiver, MemberAccessExpressionSyntax? member)
    {
        if (member is not null && DirectAmbient(member))
            return KnownTypeChecks.StandardReceiver(owner, receiver);
        return receiver is MemberAccessExpressionSyntax nested && AmbientApi.Outputs.UseAt(nested) is not null
            && KnownTypeChecks.StandardReceiver(owner, nested.Expression);
    }

    private static bool DirectAmbient(MemberAccessExpressionSyntax member) =>
        AmbientApi.Inputs.UseAt(member) is not null || AmbientApi.Outputs.UseAt(member) is not null;
}
