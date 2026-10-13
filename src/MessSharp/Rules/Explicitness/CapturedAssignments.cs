using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class CapturedAssignments
{
    public static IEnumerable<StorageBinding> Find(SyntaxNode body) => body.DescendantNodes()
        .OfType<AssignmentExpressionSyntax>().Where(assignment => Captures(assignment, body))
        .SelectMany(StorageBindings.Find).Select(binding => binding with
        {
            Source = new BindingSource.Unknown(),
            Position = body.Span.End,
            Scope = body,
            Joins = true,
        });

    private static bool Captures(AssignmentExpressionSyntax assignment, SyntaxNode body)
    {
        if (assignment.Left is not IdentifierNameSyntax name) return false;
        var nested = assignment.Ancestors().TakeWhile(node => node != body)
            .FirstOrDefault(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
        if (nested is null) return false;
        if (ExpressionTypes.Binding(name) is { } binding) return !nested.Span.Contains(binding.Span);
        return !CallableOwner.Parameters(nested).Any(p => p.Identifier.Text == name.Identifier.Text);
    }
}
