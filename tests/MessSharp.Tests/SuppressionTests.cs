using System.Diagnostics.CodeAnalysis;
using MessSharp.Cli;
using CliRunner = MessSharp.Cli.Cli;
using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.Rules.CodeSize;
using Xunit;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.Tests;

public class SuppressionTests
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

    private const string AttributeSuppressedMethodSource = @"
using System.Diagnostics.CodeAnalysis;

public class ComplexClass {
    [SuppressMessage(""MessSharp"", ""CyclomaticComplexity"")]
    [SuppressMessage(""MessSharp"", ""NPathComplexity"")]
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

    private const string CommentSuppressedMethodSource = @"
public class ComplexClass {
    // @SuppressWarnings(PHPMD.CyclomaticComplexity)
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

    private const string ClassLevelSuppressedSource = @"
using System.Diagnostics.CodeAnalysis;

[SuppressMessage(""MessSharp"", ""CyclomaticComplexity"")]
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
    public void Engine_SuppressedByAttribute_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("complex.cs", AttributeSuppressedMethodSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedByAttribute_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("complex.cs", AttributeSuppressedMethodSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    [Fact]
    public void Engine_SuppressedByComment_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("complex.cs", CommentSuppressedMethodSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedByComment_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("complex.cs", CommentSuppressedMethodSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    [Fact]
    public void Engine_SuppressedAtClassLevel_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("complex.cs", ClassLevelSuppressedSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Cli_WithSuppressedViolation_StrictFlagControlsExitCode()
    {
        var tmpFile = Path.GetTempFileName() + ".cs";
        File.WriteAllText(tmpFile, AttributeSuppressedMethodSource);
        var rulesetsDir = Path.Combine(AppContext.BaseDirectory, "rulesets");
        var codesizeXml = Path.Combine(rulesetsDir, "codesize.xml");
        try
        {
            var outW = new StringWriter();
            var errW = new StringWriter();
            int cleanCode = CliRunner.Run(new[] { tmpFile, "text", codesizeXml }, outW, errW);
            Assert.Equal(0, cleanCode);

            var outStrict = new StringWriter();
            var errStrict = new StringWriter();
            int strictCode = CliRunner.Run(new[] { tmpFile, "text", codesizeXml, "--strict" }, outStrict, errStrict);
            Assert.Equal(2, strictCode);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }
}
