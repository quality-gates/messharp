using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class KnownTypeChecks
{
    public static bool IsCustomType(CallableOwner owner, string type)
    {
        var name = type.Split('<')[0].Split('.').Last();
        return owner.Method.File.Classes.Any(c => c.Name == name);
    }

    public static bool StandardReceiver(CallableOwner owner, ExpressionSyntax receiver) =>
        !LocalShadowing.IsShadowedByLocal(receiver, receiver.ToString()) && !IsCustomType(owner, receiver.ToString());

    public static bool StandardType(CallableOwner owner, ExpressionSyntax receiver, string type) =>
        (receiver.ToString() == type || receiver.ToString() == "System." + type) && StandardReceiver(owner, receiver);
}
