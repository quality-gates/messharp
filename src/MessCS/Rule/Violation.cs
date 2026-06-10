namespace MessCS.Rule;

/// <summary>A single rule violation (mirrors PHPMD RuleViolation).</summary>
public sealed class Violation
{
    public IRule Rule { get; init; } = null!;
    public string File { get; init; } = "";
    public int BeginLine { get; init; }
    public int EndLine { get; init; }
    public string Description { get; init; } = "";
    public object[] Args { get; init; } = Array.Empty<object>();
    public string Class { get; init; } = "";
    public string Method { get; init; } = "";
    public string Function { get; init; } = "";
    public string Package { get; init; } = "";
    public int Priority { get; init; }
    public string RuleSetName { get; init; } = "";
}
