using MessSharp.Rule;

namespace MessSharp.Rules.Design;

/// <summary>
/// Design ruleset rule factories, keyed by phpmd rule class name.
/// Only edit this file from within the Design folder's owning task.
/// </summary>
public static class DesignRules
{
    public static IReadOnlyDictionary<string, Func<BaseRule>> Factories { get; } =
        new Dictionary<string, Func<BaseRule>>(StringComparer.Ordinal)
        {
            ["PHPMD\\Rule\\Design\\ExitExpression"] = () => new ExitExpressionRule(),
            ["PHPMD\\Rule\\Design\\GotoStatement"] = () => new GotoStatementRule(),
            ["PHPMD\\Rule\\Design\\CountInLoopExpression"] = () => new CountInLoopExpressionRule(),
            ["PHPMD\\Rule\\Design\\DevelopmentCodeFragment"] = () => new DevelopmentCodeFragmentRule(),
            ["PHPMD\\Rule\\Design\\EmptyCatchBlock"] = () => new EmptyCatchBlockRule(),
            ["PHPMD\\Rule\\Design\\CouplingBetweenObjects"] = () => new CouplingBetweenObjectsRule(),
            ["PHPMD\\Rule\\Design\\GlobalVariable"] = () => new GlobalVariableRule(),
            ["PHPMD\\Rule\\Design\\LackOfCohesionOfMethods"] = () => new LackOfCohesionOfMethodsRule(),
        };

    public static IReadOnlyList<IRule> All =>
        Factories.Values.Select(f => (IRule)f()).ToList();
}
