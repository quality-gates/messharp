namespace MessSharp.Rule;

/// <summary>A named collection of rules (mirrors PHPMD RuleSet).</summary>
public sealed class RuleSet
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public List<IRule> Rules { get; init; } = new();
}
