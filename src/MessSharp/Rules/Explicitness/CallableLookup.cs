using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class CallableLookup(IReadOnlyList<CallableOwner> owners)
{
    public CallableOwner? VisibleLocal(CallableOwner caller, IdentifierNameSyntax name, int arity)
    {
        for (var scope = caller; scope is not null; scope = scope.Parent)
        {
            var local = Unique(scope.Children.Where(o => IsMatchingLocal(o, name, arity)));
            if (local is not null) return local;
        }
        return null;
    }

    public CallableOwner? SameClassMethod(CallableOwner caller, IdentifierNameSyntax name, int arity) =>
        Unique(owners.Where(o => o.Parent is null && o.Method.Class == caller.Method.Class
            && o.Method.Name == name.Identifier.Text && o.Method.Parameters.Count == arity && Safe(o)));

    public CallableOwner? MemberTarget(CallableOwner caller, MemberAccessExpressionSyntax member, int arity)
    {
        var candidates = owners.Where(o => o.Parent is null && o.Method.Name == member.Name.Identifier.Text
            && o.Method.Parameters.Count == arity && Safe(o)).ToList();
        if (member.Expression is ThisExpressionSyntax)
            return Unique(candidates.Where(o => o.Method.Class == caller.Method.Class));
        if (member.Expression is not IdentifierNameSyntax type || LocalShadowing.IsShadowedByLocal(type, type.Identifier.Text))
            return null;
        return Unique(candidates.Where(o => o.Method.IsStatic() && o.Method.DeclaringTypeName == type.Identifier.Text));
    }

    private static bool IsMatchingLocal(CallableOwner owner, IdentifierNameSyntax name, int arity) =>
        owner.Method.Node is LocalFunctionStatementSyntax function
        && function.Identifier.Text == name.Identifier.Text
        && function.ParameterList.Parameters.Count == arity
        && Visible(function, name);

    private static bool Visible(SyntaxNode declaration, SyntaxNode site) =>
        declaration.Ancestors().OfType<BlockSyntax>().FirstOrDefault()?.Span.Contains(site.Span) != false;

    private static bool Safe(CallableOwner owner) => owner.Body is not null
        && owner.Method.Node is MethodDeclarationSyntax method
        && !method.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword) || m.IsKind(SyntaxKind.AbstractKeyword)
            || m.IsKind(SyntaxKind.OverrideKeyword) || m.IsKind(SyntaxKind.ExternKeyword));

    private static CallableOwner? Unique(IEnumerable<CallableOwner> candidates)
    {
        var matches = candidates.Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }
}
