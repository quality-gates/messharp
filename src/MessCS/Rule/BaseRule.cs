namespace MessCS.Rule;

/// <summary>
/// Abstract base class carrying metadata loaded from ruleset XML.
/// Concrete rule implementations embed this and only implement Apply*.
/// Mirrors messgo's rule.Base.
/// </summary>
public abstract class BaseRule : IRule
{
    public string Name { get; set; } = "";
    public string Message { get; set; } = "";
    public int Priority { get; set; } = 3;
    public string SetName { get; set; } = "";
    public string ExternalUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public string Since { get; set; } = "";
    public Properties RuleProps { get; set; } = Properties.Empty;
}
