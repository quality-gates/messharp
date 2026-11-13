using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class CapturedAssignments
{
    public static bool IsRebinding(CallableOwner owner, AssignmentExpressionSyntax assignment)
    {
        if (owner.Parent is null || owner.Body is not { } body || assignment.Left is not IdentifierNameSyntax name) return false;
        if (!OverwritesStorage(owner, assignment) || IsOrderedLazyWrite(owner, assignment)) return false;
        if (ExpressionTypes.Binding(name) is { } binding) return !body.Span.Contains(binding.Span);
        return owner.Method.Parameters.Skip(CallableOwner.Parameters(owner.Method.Node).Count())
            .Any(parameter => parameter.Name == name.Identifier.Text);
    }

    /// <summary>A write of a Lazy that the enclosing body orders at the closure's invocations, so no caller sees an unknown rebinding.</summary>
    private static bool IsOrderedLazyWrite(CallableOwner owner, AssignmentExpressionSyntax assignment) =>
        owner.Parent?.Body is { } parent && InvokedClosures.Find(parent, assignment) is not null
        && new ExpressionTypes(owner).TypeOf(assignment.Left) is { } type && type.StartsWith("Lazy<", StringComparison.Ordinal);

    private static bool OverwritesStorage(CallableOwner owner, AssignmentExpressionSyntax assignment)
    {
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && !assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression)) return false;
        var type = new ExpressionTypes(owner).TypeOf(assignment.Left);
        return type is null || SyntaxFactory.ParseTypeName(type.TrimEnd('?')) is not PredefinedTypeSyntax scalar
            || scalar.Keyword.IsKind(SyntaxKind.ObjectKeyword);
    }
}
