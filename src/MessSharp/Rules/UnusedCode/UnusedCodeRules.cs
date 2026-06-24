using MessSharp.Rule;

namespace MessSharp.Rules.UnusedCode;

/// <summary>
/// UnusedCode ruleset rule factories, keyed by phpmd rule class name.
/// Only edit this file from within the UnusedCode folder's owning task.
/// </summary>
public static class UnusedCodeRules
{
    public static IReadOnlyDictionary<string, Func<BaseRule>> Factories { get; } =
        new Dictionary<string, Func<BaseRule>>(StringComparer.Ordinal)
        {
            ["PHPMD\\Rule\\UnusedPrivateField"]     = () => new UnusedPrivateFieldRule(),
            ["PHPMD\\Rule\\UnusedLocalVariable"]    = () => new UnusedLocalVariableRule(),
            ["PHPMD\\Rule\\UnusedPrivateMethod"]    = () => new UnusedPrivateMethodRule(),
            ["PHPMD\\Rule\\UnusedFormalParameter"]  = () => new UnusedFormalParameterRule(),
        };

    public static IReadOnlyList<IRule> All =>
        Factories.Values.Select(f => (IRule)f()).ToList();
}
