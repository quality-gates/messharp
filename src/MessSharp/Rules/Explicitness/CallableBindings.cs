using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class CallableBindings
{
    public static ExpressionSyntax? Value(CallableOwner caller, IdentifierNameSyntax name)
    {
        if (ExpressionTypes.Binding(name) is not { Initializer: { } initializer } binding) return null;
        return ReassignedBeforeUse(caller, name, binding) ? null : initializer.Value;
    }

    private static bool ReassignedBeforeUse(CallableOwner caller, IdentifierNameSyntax name, VariableDeclaratorSyntax binding) =>
        caller.Body is not null && caller.Body.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(a =>
            a.Left is IdentifierNameSyntax target && target.Identifier.Text == name.Identifier.Text
                && ExpressionTypes.Binding(target) == binding && a.SpanStart > binding.SpanStart
                && (a.SpanStart < name.SpanStart || RepeatsAt(a, name) || Nested(a, caller)));

    private static bool RepeatsAt(AssignmentExpressionSyntax assignment, IdentifierNameSyntax site) =>
        assignment.Ancestors().Any(node => node is ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax
            && node.Span.Contains(site.Span));

    private static bool Nested(AssignmentExpressionSyntax assignment, CallableOwner owner) =>
        assignment.Ancestors().TakeWhile(node => node != owner.Body)
            .Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
}
