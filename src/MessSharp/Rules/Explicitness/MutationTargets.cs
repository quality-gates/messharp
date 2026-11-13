using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>The mutations explicitness tracks as state changes: initializer members and `ref` arguments are not.</summary>
internal static class MutationTargets
{
    public static IEnumerable<(ExpressionSyntax Target, bool IsPureWrite)> Find(SyntaxNode node) =>
        MutationClassifier.Classify(node)
            .Where(mutation => mutation.Form is not (MutationForm.InitializerAssignment or MutationForm.RefArgument))
            .Select(mutation => (mutation.Target, mutation.IsPureWrite));
}
