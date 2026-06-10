using MessCS.Rule;
using MessCS.Rules.CleanCode;
using MessCS.Rules.CodeSize;
using MessCS.Rules.Controversial;
using MessCS.Rules.Design;
using MessCS.Rules.Naming;
using MessCS.Rules.UnusedCode;

namespace MessCS.RuleSet;

/// <summary>
/// Maps phpmd rule class names to BaseRule factories by aggregating each
/// rule group's own Factories dictionary. To register a rule, add it to the
/// Factories map in your group's static class — never edit this file.
/// </summary>
public static class RuleFactory
{
    private static readonly Dictionary<string, Func<BaseRule>> _registry = Build();

    private static Dictionary<string, Func<BaseRule>> Build()
    {
        var groups = new[]
        {
            CleanCodeRules.Factories,
            CodeSizeRules.Factories,
            ControvRules.Factories,
            DesignRules.Factories,
            NamingRules.Factories,
            UnusedCodeRules.Factories,
        };
        var merged = new Dictionary<string, Func<BaseRule>>(StringComparer.Ordinal);
        foreach (var group in groups)
            foreach (var (name, factory) in group)
                merged[name] = factory;
        return merged;
    }

    public static BaseRule? Create(string className)
    {
        _registry.TryGetValue(className, out var factory);
        return factory?.Invoke();
    }

    public static bool IsRegistered(string className) => _registry.ContainsKey(className);
}
