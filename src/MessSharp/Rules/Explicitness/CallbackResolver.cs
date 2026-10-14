using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class CallbackResolver(IReadOnlyList<CallableOwner> owners)
{
    public CallableOwner? Find(CallableOwner caller, ExpressionSyntax actual) => Resolve(caller, actual, new HashSet<ExpressionSyntax>());

    private CallableOwner? Resolve(CallableOwner caller, ExpressionSyntax actual, HashSet<ExpressionSyntax> visiting)
    {
        actual = CallableSyntax.Unwrap(actual);
        if (!visiting.Add(actual)) return null;
        if (actual is not IdentifierNameSyntax name) return CallableNodeLookup.Find(owners, actual);
        if (new CallableLookup(owners).VisibleLocal(caller, name, null) is { } local) return local;
        if (LocalShadowing.IsShadowedByLocal(name, name.Identifier.Text))
            return CallableBindings.Value(caller, name) is { } value ? Resolve(caller, value, visiting) : null;
        return Method(caller, name);
    }

    private CallableOwner? Method(CallableOwner caller, IdentifierNameSyntax name)
    {
        var candidates = owners.Where(owner => owner.Parent is null && owner.Method.Class == caller.Method.Class
            && owner.Method.Name == name.Identifier.Text && Safe(owner)).Take(2).ToList();
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static bool Safe(CallableOwner owner) => owner.Body is not null
        && owner.Method.Node is MethodDeclarationSyntax method
        && !method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.VirtualKeyword) || modifier.IsKind(SyntaxKind.AbstractKeyword)
            || modifier.IsKind(SyntaxKind.OverrideKeyword) || modifier.IsKind(SyntaxKind.ExternKeyword));
}
