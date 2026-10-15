using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class KnownMathOperations
{
    private static readonly IReadOnlySet<string> MathCalculations = new HashSet<string>(StringComparer.Ordinal)
    {
        "Abs", "Min", "Max", "Clamp", "Round", "Floor", "Ceiling", "Truncate", "Sqrt", "Pow",
        "Sin", "Cos", "Tan", "Log", "Log10", "Exp", "Sign",
    };

    public static bool Matches(CallableOwner owner, ExpressionSyntax receiver, string name) =>
        MathCalculations.Contains(name) && (KnownTypeChecks.StandardType(owner, receiver, "Math")
            || KnownTypeChecks.StandardType(owner, receiver, "MathF"));
}
