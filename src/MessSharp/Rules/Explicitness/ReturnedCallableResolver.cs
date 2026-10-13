using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class ReturnedCallableResolver
{
    public static CallableOwner? Find(CallableOwner owner)
    {
        var candidates = new List<CallableOwner>();
        foreach (var returned in CallableSyntax.Returns(owner.Body))
        {
            var value = CallableSyntax.Unwrap(returned);
            var direct = owner.Children.FirstOrDefault(o => o.Method.Node == value);
            var target = direct ?? (value is IdentifierNameSyntax name ? NamedReturn(owner, name) : null);
            if (target is null) return null;
            candidates.Add(target);
        }
        var distinct = candidates.Distinct().Take(2).ToList();
        return distinct.Count == 1 ? distinct[0] : null;
    }

    private static CallableOwner? NamedReturn(CallableOwner owner, IdentifierNameSyntax name)
    {
        var local = owner.Children.FirstOrDefault(o => o.Method.Node is LocalFunctionStatementSyntax function
            && function.Identifier.Text == name.Identifier.Text);
        if (local is not null) return local;
        if (WasReassigned(owner, name)) return null;
        if (ExpressionTypes.Binding(name)?.Initializer is not { } initializer) return null;
        return owner.Children.FirstOrDefault(o => o.Method.Node == CallableSyntax.Unwrap(initializer.Value));
    }

    private static bool WasReassigned(CallableOwner owner, IdentifierNameSyntax name) =>
        owner.Body is not null && BodyWalk.Nodes(owner.Body).OfType<AssignmentExpressionSyntax>().Any(assignment =>
            assignment.Left is IdentifierNameSyntax target && target.Identifier.Text == name.Identifier.Text);

}
