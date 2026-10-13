using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed record DeferredExecution(string ParameterName, DeferredKind Kind)
{
    public static DeferredExecution? From(CallableOwner owner, ExpressionSyntax value, DeferredKind kind, SyntaxNode body) =>
        kind == DeferredKind.Lazy ? From(owner, value, kind, body, new HashSet<SyntaxNode>()) : null;

    private static DeferredExecution? From(CallableOwner owner, ExpressionSyntax value, DeferredKind kind,
        SyntaxNode body, HashSet<SyntaxNode> visiting)
    {
        value = CallableSyntax.Unwrap(value);
        if (!visiting.Add(value)) return null;
        if (value is IdentifierNameSyntax name && IsUnchangedParameter(owner, name, body))
            return new DeferredExecution(name.Identifier.Text, kind);
        var bindings = new DeferredValueBindings(owner, body).Values(value).ToList();
        return bindings.Count == 1 ? From(owner, bindings[0], kind, body, visiting) : null;
    }

    private static bool IsUnchangedParameter(CallableOwner owner, IdentifierNameSyntax name, SyntaxNode body) =>
        ExpressionTypes.Binding(name) is null
        && owner.Method.Parameters.Any(parameter => parameter.Name == name.Identifier.Text)
        && !body.DescendantNodesAndSelf().SelectMany(StorageBindings.Find)
            .Any(binding => binding.Name == name.Identifier.Text);
}
