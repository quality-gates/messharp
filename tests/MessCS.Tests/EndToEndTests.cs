using MessCS.Model;
using MessCS.Rule;
using MessCS.Rules.CodeSize;
using Xunit;
using RuleSetType = MessCS.Rule.RuleSet;

namespace MessCS.Tests;

/// <summary>
/// End-to-end test: engine + CyclomaticComplexity on a crafted fixture.
/// </summary>
public class EndToEndTests
{
    private static RuleSetType MakeCodeSizeSet()
    {
        var rule = new CyclomaticComplexityRule
        {
            Name = "CyclomaticComplexity",
            Message = "The {0} {1}() has a Cyclomatic Complexity of {2}. The configured cyclomatic complexity threshold is {3}.",
            Priority = 3,
            SetName = "codesize",
            ExternalUrl = "https://phpmd.org/rules/codesize.html#cyclomaticcomplexity",
            Description = "CCN rule",
            Since = "0.1",
            RuleProps = new Properties(new Dictionary<string, string> { ["reportLevel"] = "10" }),
        };
        return new RuleSetType { Name = "codesize", Rules = { rule } };
    }

    // A method with CCN > 10 (11 decision points: 5 ifs + 1 for + 3 cases + 1 &&).
    private const string HighComplexitySource = @"
public class ComplexClass {
    public int HeavyMethod(int a, int b, int c, int d, int e) {
        int x = 0;
        if (a > 0 && b > 0) { x++; }
        if (a > 1) { x++; }
        if (b > 1) { x++; }
        for (int i = 0; i < a; i++) { x++; }
        switch (c) {
            case 1: x++; break;
            case 2: x++; break;
            case 3: x++; break;
        }
        if (d > 0) { x++; }
        if (e > 0) { x++; }
        return x;
    }
}";

    [Fact]
    public void Engine_HighComplexityMethod_ReportsViolation()
    {
        var sf = ModelBuilder.Parse("complex.cs", HighComplexitySource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets);
        Assert.Single(violations);
        var v = violations[0];
        Assert.Equal("CyclomaticComplexity", v.Rule.Name);
        Assert.Equal("complex.cs", v.File);
        Assert.Contains("HeavyMethod", v.Description);
        Assert.Contains("Cyclomatic Complexity", v.Description);
    }

    [Fact]
    public void Engine_LowComplexityMethod_NoViolation()
    {
        var src = @"
public class SimpleClass {
    public int Add(int a, int b) { return a + b; }
}";
        var sf = ModelBuilder.Parse("simple.cs", src);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_ViolationMessage_MatchesPhpmdTemplate()
    {
        var sf = ModelBuilder.Parse("complex.cs", HighComplexitySource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets);
        Assert.Single(violations);
        var v = violations[0];
        // Message must follow the phpmd template:
        // "The {0} {1}() has a Cyclomatic Complexity of {2}. The configured cyclomatic complexity threshold is {3}."
        Assert.Matches(@"^The method HeavyMethod\(\) has a Cyclomatic Complexity of \d+\. The configured cyclomatic complexity threshold is 10\.$",
            v.Description);
    }
}
