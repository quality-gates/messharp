using MessSharp.Rule;
using MessSharp.Rules.Naming;
using MessSharp.Rules.UnusedCode;
using Xunit;

namespace MessSharp.Tests;

/// <summary>
/// Naming and unused-code rules discover locals through one shared walk, so a
/// declaration form must be seen by both.
/// </summary>
public class LocalDeclarationConsistencyTests
{
    private static T Make<T>(string name) where T : BaseRule, new() => new()
    {
        Name = name,
        Message = "{0}",
        Priority = 3,
        SetName = "test",
        ExternalUrl = "",
        Description = "",
        Since = "0.1",
        RuleProps = new Properties(null),
    };

    private static HashSet<string> Names(string source, BaseRule rule) =>
        Engine.Analyze(source, new[] { rule })
            .Select(v => (string)v.Args[0]).ToHashSet();

    [Fact]
    public void EveryDeclarationForm_IsSeenByNamingAndUnusedRules()
    {
        const string source = @"
class Foo
{
    void M(object o, int[] xs, (int, int)[] pairs)
    {
        int a = 1;
        var (b, c) = (1, 2);
        int.TryParse(""1"", out var d);
        foreach (var e in xs) { }
        if (o is string f) { }
        foreach (var (g, h) in pairs) { }
        (int i, int j) = (1, 2);
    }
}";
        var expected = new HashSet<string> { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" };

        var shortNames = Names(source, Make<ShortVariableRule>("ShortVariable"));
        var unusedNames = Names(source, Make<UnusedLocalVariableRule>("UnusedLocalVariable"));

        Assert.Superset(expected, shortNames);
        Assert.Superset(expected, unusedNames);
    }

    [Fact]
    public void CatchVariable_IsUnusedButNotNamingChecked()
    {
        const string source = @"
class Foo
{
    void M()
    {
        try { } catch (System.Exception e) { }
    }
}";
        Assert.Contains("e", Names(source, Make<UnusedLocalVariableRule>("UnusedLocalVariable")));
        Assert.DoesNotContain("e", Names(source, Make<ShortVariableRule>("ShortVariable")));
    }
}
