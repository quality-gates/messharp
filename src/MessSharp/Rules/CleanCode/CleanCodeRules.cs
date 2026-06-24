using MessSharp.Rule;

namespace MessSharp.Rules.CleanCode;

/// <summary>
/// CleanCode ruleset rule factories, keyed by phpmd rule class name.
/// Only edit this file from within the CleanCode folder's owning task.
/// </summary>
public static class CleanCodeRules
{
    public static IReadOnlyDictionary<string, Func<BaseRule>> Factories { get; } =
        new Dictionary<string, Func<BaseRule>>(StringComparer.Ordinal)
        {
            ["PHPMD\\Rule\\CleanCode\\BooleanArgumentFlag"] = () => new BooleanArgumentFlagRule(),
            ["PHPMD\\Rule\\CleanCode\\ElseExpression"]      = () => new ElseExpressionRule(),
            ["PHPMD\\Rule\\CleanCode\\IfStatementAssignment"] = () => new IfStatementAssignmentRule(),
            ["PHPMD\\Rule\\CleanCode\\DuplicatedArrayKey"]  = () => new DuplicatedArrayKeyRule(),
            ["PHPMD\\Rule\\CleanCode\\StaticAccess"]        = () => new StaticAccessRule(),
        };

    public static IReadOnlyList<IRule> All =>
        Factories.Values.Select(f => (IRule)f()).ToList();
}
