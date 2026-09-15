using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Metrics;

internal static class NPathDeclarationMetrics
{
    internal static int Complexity(VariableDeclarationSyntax declaration)
    {
        int complexity = InitializerComplexity(declaration);
        return complexity == 0 ? 1 : complexity;
    }

    internal static int InitializerComplexity(VariableDeclarationSyntax? declaration)
    {
        if (declaration == null) return 0;

        int complexity = 0;
        foreach (var variable in declaration.Variables)
            complexity = NPathArithmetic.Add(
                complexity,
                NPathExpressionMetrics.Complexity(variable.Initializer?.Value));
        return complexity;
    }
}
