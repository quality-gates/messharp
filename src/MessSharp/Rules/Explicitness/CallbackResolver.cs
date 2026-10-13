using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class CallbackResolver(IReadOnlyList<CallableOwner> owners)
{
    public CallableOwner? Find(CallableOwner caller, ExpressionSyntax actual)
    {
        actual = CallableSyntax.Unwrap(actual);
        if (actual is not IdentifierNameSyntax name) return CallableNodeLookup.Find(owners, actual);
        if (LocalShadowing.IsShadowedByLocal(name, name.Identifier.Text)) return null;
        var candidates = owners.Where(owner => owner.Parent is null && owner.Method.Class == caller.Method.Class
            && owner.Method.Name == name.Identifier.Text && Safe(owner)).Take(2).ToList();
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static bool Safe(CallableOwner owner) => owner.Body is not null
        && owner.Method.Node is MethodDeclarationSyntax method
        && !method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.VirtualKeyword) || modifier.IsKind(SyntaxKind.AbstractKeyword)
            || modifier.IsKind(SyntaxKind.OverrideKeyword) || modifier.IsKind(SyntaxKind.ExternKeyword));
}
