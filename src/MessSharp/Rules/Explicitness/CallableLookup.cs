using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class CallableLookup(IReadOnlyList<CallableOwner> owners)
{
    public CallableOwner? VisibleLocal(CallableOwner caller, IdentifierNameSyntax name, CallShape? shape)
    {
        for (var scope = caller; scope is not null; scope = scope.Parent)
        {
            var locals = scope.Children.Where(o => IsMatchingLocal(o, name));
            var local = shape is null ? Unique(locals) : shape.Best(locals);
            if (local is not null) return local;
        }
        return null;
    }

    /// <summary>The callable a method group names when converted to a delegate taking <paramref name="arity"/> arguments.</summary>
    public CallableOwner? Group(CallableOwner caller, ExpressionSyntax group, int arity) => group switch
    {
        IdentifierNameSyntax name => VisibleLocal(caller, name, new CallShape(arity)) ?? SameClassMethod(caller, name, new CallShape(arity)),
        MemberAccessExpressionSyntax member => MemberTarget(caller, member, new CallShape(arity)),
        _ => null,
    };

    public CallableOwner? SameClassMethod(CallableOwner caller, IdentifierNameSyntax name, CallShape shape) =>
        shape.Best(owners.Where(o => o.Parent is null && o.Method.Class == caller.Method.Class
            && o.Method.Name == name.Identifier.Text && Safe(o)));

    public CallableOwner? MemberTarget(CallableOwner caller, MemberAccessExpressionSyntax member, CallShape shape)
    {
        var candidates = owners.Where(o => o.Parent is null && o.Method.Name == member.Name.Identifier.Text && Safe(o)).ToList();
        if (member.Expression is ThisExpressionSyntax)
            return shape.Best(candidates.Where(o => o.Method.Class == caller.Method.Class));
        return StaticTarget(member.Expression, candidates, shape) ?? ReceiverTarget(caller, member.Expression, candidates, shape);
    }

    private static CallableOwner? StaticTarget(ExpressionSyntax receiver, List<CallableOwner> candidates, CallShape shape) =>
        receiver is IdentifierNameSyntax type && !LocalShadowing.IsShadowedByLocal(type, type.Identifier.Text)
            ? shape.Best(candidates.Where(o => o.Method.IsStatic() && o.Method.DeclaringTypeName == type.Identifier.Text))
            : null;

    /// <summary>An instance method of the receiver's type, or else an extension method on it.</summary>
    private CallableOwner? ReceiverTarget(CallableOwner caller, ExpressionSyntax expression, List<CallableOwner> candidates,
        CallShape shape)
    {
        if (new ExpressionTypes(caller, owners).TypeOf(expression) is not { } receiver) return null;
        return shape.Best(candidates.Where(o => IsInstanceOf(o, receiver)))
            ?? shape.Best(candidates.Where(o => IsExtensionOf(o, receiver)), 1);
    }

    /// <summary>A non-virtual instance method of the receiver's own class binds statically.</summary>
    private static bool IsInstanceOf(CallableOwner owner, string receiver) => !owner.Method.IsStatic()
        && owner.Method.Class?.Node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax
        && owner.Method.Class.Name == TypeNames.Simple(receiver);

    private static bool IsExtensionOf(CallableOwner owner, string receiver) => ExtensionMethods.IsExtension(owner)
        && ExtensionMethods.ReceiverType(owner) is { } declared && TypeNames.Simple(declared) == TypeNames.Simple(receiver);

    private static bool IsMatchingLocal(CallableOwner owner, IdentifierNameSyntax name) =>
        owner.Method.Node is LocalFunctionStatementSyntax function
        && function.Identifier.Text == name.Identifier.Text
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
