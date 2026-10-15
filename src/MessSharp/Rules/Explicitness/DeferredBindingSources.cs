using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The sources a declaration starts with, and the sources one of its bindings stores.</summary>
internal static class DeferredBindingSources
{
    public static IEnumerable<DeferredSource> Initial(SyntaxNode declaration) => declaration switch
    {
        VariableDeclaratorSyntax { Initializer: { } initializer } => [new DeferredSource.Value(initializer.Value)],
        PropertyDeclarationSyntax { Initializer: { } initializer } => [new DeferredSource.Value(initializer.Value)],
        ParameterSyntax parameter => [new DeferredSource.Entry(parameter.Identifier.Text)],
        _ => [],
    };

    public static IEnumerable<DeferredSource> Stored(StorageBinding binding) => (binding.Node, binding.Source) switch
    {
        (_, BindingSource.Value value) => [new DeferredSource.Value(value.Expression)],
        (_, BindingSource.Element element) => [new DeferredSource.Element(element.Expression)],
        (_, BindingSource.Private) => [],
        (ArgumentSyntax argument, BindingSource.Unknown) => [new DeferredSource.Written(argument)],
        (SingleVariableDesignationSyntax { Parent: DeclarationExpressionSyntax { Parent: ArgumentSyntax argument } },
            BindingSource.Unknown) => [new DeferredSource.Written(argument)],
        _ => [new DeferredSource.Opaque()],
    };
}
