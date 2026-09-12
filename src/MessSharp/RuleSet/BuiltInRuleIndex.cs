namespace MessSharp.RuleSet;

/// <summary>
/// Indexes rule names across built-in rulesets to resolve bare rule references
/// like &lt;rule ref="LongVariable"&gt;.
/// </summary>
internal static class BuiltInRuleIndex
{
    private static readonly Dictionary<string, string> RuleToRuleset = new(StringComparer.OrdinalIgnoreCase);

    static BuiltInRuleIndex()
    {
        BuildIndex();
    }

    private static void BuildIndex()
    {
        string[] baseRulesets =
        {
            "cleancode",
            "codesize",
            "controversial",
            "design",
            "naming",
            "unusedcode",
        };

        foreach (var ruleset in baseRulesets)
        {
            var data = BuiltInRuleSetReader.Read($"{ruleset}.xml");
            if (data == null) continue;

            var xmlRuleset = XmlRulesetParser.Deserialize(data);
            if (xmlRuleset.Rules == null) continue;

            foreach (var rule in xmlRuleset.Rules)
            {
                if (!string.IsNullOrEmpty(rule.Name))
                    RuleToRuleset[rule.Name] = ruleset;
            }
        }
    }

    public static bool TryGetRuleset(string ruleName, out string ruleset) =>
        RuleToRuleset.TryGetValue(ruleName, out ruleset!);
}
