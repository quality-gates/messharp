using MessSharp.Rule;

namespace MessSharp.Rules.CodeSize;

/// <summary>
/// CodeSize ruleset rule factories, keyed by phpmd rule class name.
/// Only edit this file from within the CodeSize folder's owning task.
/// </summary>
public static class CodeSizeRules
{
    public static IReadOnlyDictionary<string, Func<BaseRule>> Factories { get; } =
        new Dictionary<string, Func<BaseRule>>(StringComparer.Ordinal)
        {
            ["PHPMD\\Rule\\CyclomaticComplexity"] = () => new CyclomaticComplexityRule(),
            ["PHPMD\\Rule\\Design\\NpathComplexity"] = () => new NPathComplexityRule(),
            ["PHPMD\\Rule\\Design\\LongMethod"] = () => new ExcessiveMethodLengthRule(),
            ["PHPMD\\Rule\\Design\\LongClass"] = () => new ExcessiveClassLengthRule(),
            ["PHPMD\\Rule\\Design\\LongParameterList"] = () => new ExcessiveParameterListRule(),
            ["PHPMD\\Rule\\ExcessivePublicCount"] = () => new ExcessivePublicCountRule(),
            ["PHPMD\\Rule\\Design\\TooManyFields"] = () => new TooManyFieldsRule(),
            ["PHPMD\\Rule\\Design\\TooManyMethods"] = () => new TooManyMethodsRule(),
            ["PHPMD\\Rule\\Design\\TooManyPublicMethods"] = () => new TooManyPublicMethodsRule(),
            ["PHPMD\\Rule\\Design\\WeightedMethodCount"] = () => new ExcessiveClassComplexityRule(),
        };

    public static IReadOnlyList<IRule> All =>
        Factories.Values.Select(f => (IRule)f()).ToList();
}
