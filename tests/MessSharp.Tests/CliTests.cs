using Xunit;
using CliRunner = MessSharp.Cli.Cli;

namespace MessSharp.Tests;

public class CliTests
{
    private static (int code, string stdout, string stderr) RunCli(params string[] args)
    {
        var outW = new StringWriter();
        var errW = new StringWriter();
        int code = CliRunner.Run(args, outW, errW);
        return (code, outW.ToString(), errW.ToString());
    }

    [Fact]
    public void NoArgs_ReturnsError()
    {
        var (code, _, stderr) = RunCli();
        Assert.Equal(1, code);
        Assert.Contains("Usage", stderr);
    }

    [Fact]
    public void VersionFlag_PrintsVersion()
    {
        var (code, stdout, _) = RunCli("--version");
        Assert.Equal(0, code);
        Assert.Equal($"messharp {BuildInfo.Version}{Environment.NewLine}", stdout);
    }

    [Fact]
    public void HelpFlag_PrintsUsage()
    {
        var (code, stdout, _) = RunCli("--help");
        Assert.Equal(0, code);
        Assert.Contains("Usage", stdout);
    }

    [Fact]
    public void HelpFlag_AfterOtherArgs_PrintsUsage()
    {
        var (code, stdout, stderr) = RunCli("--verbose", "--help");
        Assert.Equal(0, code);
        Assert.Contains("Usage", stdout);
        Assert.DoesNotContain("unknown option", stderr);
    }

    [Fact]
    public void HelpFlag_AfterPositionals_PrintsUsage()
    {
        var (code, stdout, stderr) = RunCli("somepath", "text", "csharp", "--help");
        Assert.Equal(0, code);
        Assert.Contains("Usage", stdout);
        Assert.DoesNotContain("unknown option", stderr);
    }

    [Fact]
    public void ShortHelpFlag_AfterOtherArgs_PrintsUsage()
    {
        var (code, stdout, stderr) = RunCli("--verbose", "-h");
        Assert.Equal(0, code);
        Assert.Contains("Usage", stdout);
        Assert.DoesNotContain("unknown option", stderr);
    }

    [Fact]
    public void ShortHelpFlag_AfterPositionals_PrintsUsage()
    {
        var (code, stdout, _) = RunCli("somepath", "text", "csharp", "-h");
        Assert.Equal(0, code);
        Assert.Contains("Usage", stdout);
    }

    [Fact]
    public void VersionFlag_AfterOtherArgs_PrintsVersion()
    {
        var (code, stdout, stderr) = RunCli("--verbose", "--version");
        Assert.Equal(0, code);
        Assert.Equal($"messharp {BuildInfo.Version}{Environment.NewLine}", stdout);
        Assert.DoesNotContain("unknown option", stderr);
    }

    [Fact]
    public void UnknownOption_ReturnsError()
    {
        var (code, _, stderr) = RunCli("--nonexistent-flag");
        Assert.Equal(1, code);
        Assert.Contains("unknown option", stderr);
    }

    [Fact]
    public void TooFewPositionals_ReturnsError()
    {
        var (code, _, stderr) = RunCli("somepath", "text");
        Assert.Equal(1, code);
        Assert.Contains("Usage", stderr);
    }

    [Fact]
    public void UnknownFormat_ReturnsError()
    {
        // Use a path that exists (temp file) so we get past path resolution
        var tmpFile = Path.GetTempFileName();
        File.WriteAllText(tmpFile, "class X{}");
        try
        {
            var (code, _, stderr) = RunCli(tmpFile, "badformat", "codesize");
            Assert.Equal(1, code);
            Assert.Contains("unknown report format", stderr);
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public void CleanFile_ExitsZero()
    {
        var tmpFile = Path.GetTempFileName() + ".cs";
        File.WriteAllText(tmpFile, "public class SimpleClass { public int Add(int a, int b) { return a + b; } }");
        var rulesetsDir = Path.Combine(AppContext.BaseDirectory, "rulesets");
        try
        {
            var (code, _, _) = RunCli(tmpFile, "text", Path.Combine(rulesetsDir, "codesize.xml"));
            Assert.Equal(0, code);
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public void Explicitness_SequenceConsumersTransferIteratorEffects()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-cli-");
        try
        {
            var sourceFile = Path.Combine(directory.FullName, "Probe.cs");
            File.WriteAllText(sourceFile, """
                using System;
                using System.Collections.Generic;
                using System.Linq;
                class Probe {
                    static int Read() => Console.Read();
                    static IEnumerable<int> Gen() { yield return Read(); }
                    static IEnumerable<int> Pure() { yield return 7; }
                    static bool ContainsAction() => Gen().Contains(65);
                    static bool ContainsPure() => Pure().Contains(65);
                    static int[] SpreadAction() => [.. Gen()];
                    static int[] SpreadPure() => [.. Pure()];
                    static List<int> ListAction() => new List<int>(Gen());
                    static List<int> ListPure() => new List<int>(Pure());
                    static void AddAction() { var xs = new List<int>(); xs.AddRange(Gen()); }
                    static void AddPure() { var xs = new List<int>(); xs.AddRange(Pure()); }
                }
                """);

            var (code, stdout, stderr) = RunCli(sourceFile, "text", "explicitness", "--strict");

            Assert.Equal(2, code);
            Assert.Empty(stderr);
            foreach (var (name, line) in new[] { ("ContainsAction", 8), ("SpreadAction", 10), ("ListAction", 12), ("AddAction", 14) })
                Assert.Matches($@"Probe\.cs:{line}\s+ImplicitInput\s+The method {name}\(\) has an implicit input: uses Console\.Read\.", stdout);
            foreach (var name in new[] { "ContainsPure", "SpreadPure", "ListPure", "AddPure" })
                Assert.DoesNotContain($"The method {name}()", stdout);
            Assert.DoesNotContain("UnknownEffect", stdout);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void Explicitness_ContainsForcesColdQueryAtTheConsumer()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-cli-");
        try
        {
            var sourceFile = Path.Combine(directory.FullName, "Probe.cs");
            File.WriteAllText(sourceFile, """
                using System;
                using System.Linq;
                class Probe {
                    static int Read() => Console.Read();
                    static bool Action() {
                        var q = new[] { 1 }.Select(x => Read());
                        return q.Contains(65);
                    }
                    static bool Pure() {
                        var q = new[] { 1 }.Select(x => 7);
                        return q.Contains(65);
                    }
                }
                """);

            var (code, stdout, stderr) = RunCli(sourceFile, "text", "explicitness", "--strict");

            Assert.Equal(2, code);
            Assert.Empty(stderr);
            Assert.Matches(@"Probe\.cs:7\s+ImplicitInput\s+The method Action\(\) has an implicit input: uses Console\.Read\.", stdout);
            Assert.DoesNotContain("The method Action() has an unknown effect", stdout);
            Assert.DoesNotContain("The method Pure()", stdout);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void Explicitness_ListConstructionDoesNotLoseCrossFileAliasSequences()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-cli-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Aliases.cs"),
                "global using GlobalSeq = System.Collections.Generic.IEnumerable<int>;");
            File.WriteAllText(Path.Combine(directory.FullName, "Probe.cs"), """
                using System;
                using System.Collections.Generic;
                class Probe {
                    static IEnumerable<int> Gen() { yield return Console.Read(); }
                    static List<int> Action() {
                        GlobalSeq sequence = Gen();
                        return new List<int>(sequence);
                    }
                    static List<int> Opaque(GlobalSeq sequence) => new List<int>(sequence);
                }
                """);

            var (code, stdout, stderr) = RunCli(directory.FullName, "text", "explicitness", "--strict");

            Assert.Equal(2, code);
            Assert.Empty(stderr);
            Assert.Matches(@"Probe\.cs:7\s+ImplicitInput\s+The method Action\(\) has an implicit input: uses Console\.Read\.", stdout);
            Assert.Matches(@"Probe\.cs:9\s+UnknownEffect\s+The method Opaque\(\) has an unknown effect: unresolved deferred execution\.", stdout);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void GlobalVariable_QueryRangeDoesNotHideLaterFieldWrite()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-cli-");
        try
        {
            var sourceFile = Path.Combine(directory.FullName, "Probe.cs");
            File.WriteAllText(sourceFile, """
                using System.Linq;
                class Probe {
                    static int x;
                    public static void Run(int[] values) {
                        var q = from x in values select x;
                        x = 1;
                    }
                }
                """);

            var (code, stdout, stderr) = RunCli(sourceFile, "text", "csharp", "--only", "GlobalVariable", "--strict");

            Assert.Equal(2, code);
            Assert.Empty(stderr);
            Assert.Contains("GlobalVariable", stdout);
            Assert.Contains("Avoid using static mutable state: x.", stdout);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void GlobalVariable_OutVariableInJoinSourceRemainsLocalAfterQuery()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-cli-");
        try
        {
            var sourceFile = Path.Combine(directory.FullName, "Probe.cs");
            File.WriteAllText(sourceFile, """
                using System.Linq;
                class Probe {
                    static int x;
                    static int[] Source(out int value) { value = 1; return new[] { 1 }; }
                    public static int Run(int[] values) {
                        var q = from a in values join b in Source(out var x) on a equals b select a;
                        x = 2;
                        return x;
                    }
                }
                """);

            var (code, stdout, stderr) = RunCli(sourceFile, "text", "csharp", "--only", "GlobalVariable", "--strict");

            Assert.Equal(0, code);
            Assert.Empty(stdout);
            Assert.Empty(stderr);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("checked")]
    [InlineData("unchecked")]
    public void NPath_ArithmeticCheckingWrappersPreservePaths(string wrapper)
    {
        var directory = Directory.CreateTempSubdirectory("messharp-cli-");
        try
        {
            var sourceFile = Path.Combine(directory.FullName, "Sample.cs");
            var rulesetFile = Path.Combine(directory.FullName, "npath.xml");
            const string body = "if (a) { } if (b) { } if (c) { }";
            var subjectBody = wrapper == "" ? body : $"{wrapper} {{ {body} }}";
            File.WriteAllText(sourceFile, $@"public class Sample {{
    public void Baseline(bool a, bool b, bool c) {{ {body} }}
    public void Subject(bool a, bool b, bool c) {{ {subjectBody} }}
}}");
            File.WriteAllText(rulesetFile, @"<ruleset name=""NPath"">
  <rule ref=""codesize/NPathComplexity"">
    <properties><property name=""minimum"" value=""1"" /></properties>
  </rule>
</ruleset>");

            var (code, stdout, stderr) = RunCli(sourceFile, "text", rulesetFile);

            Assert.Equal(2, code);
            Assert.Empty(stderr);
            Assert.Contains("The method Baseline() has an NPath complexity of 8.", stdout);
            var match = System.Text.RegularExpressions.Regex.Match(stdout,
                @"The method Subject\(\) has an NPath complexity of (\d+)\.");
            Assert.True(match.Success, stdout);
            Assert.Equal("8", match.Groups[1].Value);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Theory]
    [InlineData("checked")]
    [InlineData("unchecked")]
    public void NPath_ArithmeticCheckingWrappersComposeWithSurroundingStatements(string wrapper)
    {
        var directory = Directory.CreateTempSubdirectory("messharp-cli-");
        try
        {
            var sourceFile = Path.Combine(directory.FullName, "Sample.cs");
            var rulesetFile = Path.Combine(directory.FullName, "npath.xml");
            File.WriteAllText(sourceFile, $@"public class Sample {{
    public void Baseline(bool a, bool b, bool c, bool d) {{
        if (a) {{ }} if (b) {{ }} if (c) {{ }} if (d) {{ }}
    }}
    public void Subject(bool a, bool b, bool c, bool d) {{
        if (a) {{ }} {wrapper} {{ if (b) {{ }} if (c) {{ }} }} if (d) {{ }}
    }}
}}");
            File.WriteAllText(rulesetFile, @"<ruleset name=""NPath"">
  <rule ref=""codesize/NPathComplexity"">
    <properties><property name=""minimum"" value=""1"" /></properties>
  </rule>
</ruleset>");

            var (code, stdout, stderr) = RunCli(sourceFile, "text", rulesetFile);

            Assert.Equal(2, code);
            Assert.Empty(stderr);
            Assert.Contains("The method Baseline() has an NPath complexity of 16.", stdout);
            Assert.Contains("The method Subject() has an NPath complexity of 16.", stdout);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Theory]
    [InlineData("checked")]
    [InlineData("unchecked")]
    public void NPath_ArithmeticCheckingWrappersPreserveSaturation(string wrapper)
    {
        var directory = Directory.CreateTempSubdirectory("messharp-cli-");
        try
        {
            var sourceFile = Path.Combine(directory.FullName, "Sample.cs");
            var rulesetFile = Path.Combine(directory.FullName, "npath.xml");
            var innerDecisions = string.Join(" ", Enumerable.Repeat("if (condition) { }", 30));
            File.WriteAllText(sourceFile, $@"public class Sample {{
    public void Baseline(bool condition) {{
        if (condition) {{ }} {innerDecisions} if (condition) {{ }}
    }}
    public void Subject(bool condition) {{
        if (condition) {{ }} {wrapper} {{ {innerDecisions} }} if (condition) {{ }}
    }}
}}");
            File.WriteAllText(rulesetFile, @"<ruleset name=""NPath"">
  <rule ref=""codesize/NPathComplexity"">
    <properties><property name=""minimum"" value=""1"" /></properties>
  </rule>
</ruleset>");

            var (code, stdout, stderr) = RunCli(sourceFile, "text", rulesetFile);

            Assert.Equal(2, code);
            Assert.Empty(stderr);
            Assert.Contains("The method Baseline() has an NPath complexity of 2147483647.", stdout);
            Assert.Contains("The method Subject() has an NPath complexity of 2147483647.", stdout);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void MissingPath_ReturnsError()
    {
        var (code, _, stderr) = RunCli("./does-not-exist-anywhere", "text", "codesize");
        Assert.Equal(1, code);
        Assert.Contains("no such file or directory", stderr);
    }

    [Fact]
    public void RulesetWithUnresolvableRulesetRef_ExitsWithError()
    {
        var srcFile = Path.GetTempFileName() + ".cs";
        var rulesetFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(srcFile, "public class Aa { }");
        File.WriteAllText(rulesetFile, @"<?xml version=""1.0""?>
<ruleset name=""BadRef1"">
  <rule ref=""nosuchset/SomeRule""/>
</ruleset>");
        try
        {
            var (code, _, stderr) = RunCli(srcFile, "text", rulesetFile);
            Assert.Equal(1, code);
            Assert.Contains("error:", stderr);
        }
        finally
        {
            File.Delete(srcFile);
            File.Delete(rulesetFile);
        }
    }

    [Fact]
    public void RulesetWithUnknownRuleRef_ExitsWithError()
    {
        var srcFile = Path.GetTempFileName() + ".cs";
        var rulesetFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(srcFile, "public class Aa { }");
        File.WriteAllText(rulesetFile, @"<?xml version=""1.0""?>
<ruleset name=""BadRef2"">
  <rule ref=""naming/NoSuchRule""/>
</ruleset>");
        try
        {
            var (code, _, stderr) = RunCli(srcFile, "text", rulesetFile);
            Assert.Equal(1, code);
            Assert.Contains("error:", stderr);
        }
        finally
        {
            File.Delete(srcFile);
            File.Delete(rulesetFile);
        }
    }

    [Fact]
    public void RulesetWithCyclicRefs_ExitsWithError()
    {
        var srcFile = Path.GetTempFileName() + ".cs";
        var aFile = Path.GetTempFileName() + ".xml";
        var bFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(srcFile, "public class Aa { }");
        File.WriteAllText(aFile, $@"<?xml version=""1.0""?>
<ruleset name=""a"" xmlns=""http://pmd.sf.net/ruleset/1.0.0"">
  <rule ref=""{bFile}""/>
</ruleset>");
        File.WriteAllText(bFile, $@"<?xml version=""1.0""?>
<ruleset name=""b"" xmlns=""http://pmd.sf.net/ruleset/1.0.0"">
  <rule ref=""{aFile}""/>
</ruleset>");
        try
        {
            var (code, _, stderr) = RunCli(srcFile, "text", aFile);
            Assert.Equal(1, code);
            Assert.Contains("Cyclic ruleset ref", stderr);
        }
        finally
        {
            File.Delete(srcFile);
            File.Delete(aFile);
            File.Delete(bFile);
        }
    }

    [Fact]
    public void RulesetWithBareRuleRef_Succeeds()
    {
        var srcFile = Path.GetTempFileName() + ".cs";
        var rulesetFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(srcFile, "public class SampleClass { public void DoWork() { } }");
        File.WriteAllText(rulesetFile, @"<?xml version=""1.0""?>
<ruleset name=""TeamPolicy"">
  <rule ref=""csharp"">
    <exclude name=""DevelopmentCodeFragment""/>
  </rule>
  <rule ref=""LongVariable"">
    <priority>2</priority>
    <properties>
      <property name=""maximum"" value=""50""/>
    </properties>
  </rule>
</ruleset>");
        try
        {
            var (code, _, stderr) = RunCli(srcFile, "text", rulesetFile);
            Assert.Equal(0, code);
            Assert.Empty(stderr);
        }
        finally
        {
            File.Delete(srcFile);
            File.Delete(rulesetFile);
        }
    }

    [Fact]
    public void RulesetWithInvalidPriority_EmitsVerboseWarningAndDropsRule()
    {
        var srcFile = Path.GetTempFileName() + ".cs";
        var rulesetFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(srcFile, "public class Sample { public void DoWork(bool flag) { if (flag) { } } }");
        File.WriteAllText(rulesetFile, @"<ruleset name=""Custom"">
  <rule name=""CyclomaticComplexity"" message=""BADPRIO"" class=""PHPMD\Rule\CyclomaticComplexity"">
    <priority>garbage</priority>
    <properties>
      <property name=""reportLevel"" value=""1""/>
    </properties>
  </rule>
</ruleset>");
        try
        {
            var (code, stdout, stderr) = RunCli(srcFile, "text", rulesetFile, "--verbose");

            Assert.Equal(0, code);
            Assert.Empty(stdout);
            Assert.Contains("warning:", stderr);
            Assert.Contains("CyclomaticComplexity", stderr);
            Assert.Contains("garbage", stderr);
        }
        finally
        {
            File.Delete(srcFile);
            File.Delete(rulesetFile);
        }
    }

    [Fact]
    public void CSharpRuleset_LongLambdaParameter_UsesConfiguredMaximum()
    {
        var srcFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
        const string parameterName = "thisIsAnExtremelyLongLambdaParameterNameForTheDefaultCSharpRuleset";
        File.WriteAllText(srcFile, $@"
public class SampleClass
{{
    public void Run()
    {{
        new System.Action<int>({parameterName} => {{ }});
    }}
}}");
        try
        {
            var (code, stdout, stderr) = RunCli(srcFile, "text", "csharp", "--only", "LongVariable");

            Assert.Equal(2, code);
            Assert.Empty(stderr);
            Assert.Contains("LongVariable", stdout);
            Assert.Contains(parameterName, stdout);
            Assert.Contains("under 35", stdout);
        }
        finally
        {
            File.Delete(srcFile);
        }
    }

    [Fact]
    public void RulesetWithQualifiedRuleOverride_HonorsOverriddenProperties()
    {
        var srcFile = Path.GetTempFileName() + ".cs";
        var rulesetFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(srcFile, "public class SampleClass { private int aVeryLongFieldNameWithThirtyEightChars; }");
        File.WriteAllText(rulesetFile, @"<?xml version=""1.0""?>
<ruleset name=""TeamPolicy"">
  <rule ref=""csharp""/>
  <rule ref=""naming/LongVariable"">
    <priority>2</priority>
    <properties>
      <property name=""maximum"" value=""50""/>
    </properties>
  </rule>
</ruleset>");
        try
        {
            // First verify csharp alone produces exit code 2 with max 35
            var (baseCode, baseOut, _) = RunCli(srcFile, "text", "csharp", "--only", "LongVariable");
            Assert.Equal(2, baseCode);
            Assert.Contains("Keep variable name length under 35", baseOut);

            // Verify overridden ruleset permits 38-char name under max 50
            var (code, stdout, stderr) = RunCli(srcFile, "text", rulesetFile, "--only", "LongVariable");
            Assert.Equal(0, code);
            Assert.Empty(stderr);
            Assert.Empty(stdout);

            // Verify overridden ruleset reports violation when length exceeds 50
            File.WriteAllText(srcFile, "public class SampleClass { private int aVeryLongFieldNameWithFiftyFiveCharactersExceedingFifty; }");
            var (violationCode, violationOut, _) = RunCli(srcFile, "text", rulesetFile, "--only", "LongVariable");
            Assert.Equal(2, violationCode);
            Assert.Contains("Keep variable name length under 50", violationOut);
        }
        finally
        {
            File.Delete(srcFile);
            File.Delete(rulesetFile);
        }
    }

    [Fact]
    public void RulesetWithBareRuleOverride_HonorsOverriddenProperties()
    {
        var srcFile = Path.GetTempFileName() + ".cs";
        var rulesetFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(srcFile, "public class SampleClass { private int aVeryLongFieldNameWithThirtyEightChars; }");
        File.WriteAllText(rulesetFile, @"<?xml version=""1.0""?>
<ruleset name=""TeamPolicy"">
  <rule ref=""csharp""/>
  <rule ref=""LongVariable"">
    <priority>2</priority>
    <properties>
      <property name=""maximum"" value=""50""/>
    </properties>
  </rule>
</ruleset>");
        try
        {
            // First verify csharp alone produces exit code 2 with max 35
            var (baseCode, baseOut, _) = RunCli(srcFile, "text", "csharp", "--only", "LongVariable");
            Assert.Equal(2, baseCode);
            Assert.Contains("Keep variable name length under 35", baseOut);

            // Verify overridden ruleset permits 38-char name under max 50
            var (code, stdout, stderr) = RunCli(srcFile, "text", rulesetFile, "--only", "LongVariable");
            Assert.Equal(0, code);
            Assert.Empty(stderr);
            Assert.Empty(stdout);

            // Verify overridden ruleset reports violation when length exceeds 50
            File.WriteAllText(srcFile, "public class SampleClass { private int aVeryLongFieldNameWithFiftyFiveCharactersExceedingFifty; }");
            var (violationCode, violationOut, _) = RunCli(srcFile, "text", rulesetFile, "--only", "LongVariable");
            Assert.Equal(2, violationCode);
            Assert.Contains("Keep variable name length under 50", violationOut);
        }
        finally
        {
            File.Delete(srcFile);
            File.Delete(rulesetFile);
        }
    }

    [Fact]
    public void CommaSeparatedRulesets_LaterRulesetOverridesPriorRule()
    {
        var srcFile = Path.GetTempFileName() + ".cs";
        var rulesetFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(srcFile, "public class SampleClass { private int aVeryLongFieldNameWithThirtyEightChars; }");
        File.WriteAllText(rulesetFile, @"<?xml version=""1.0""?>
<ruleset name=""CustomOverride"">
  <rule ref=""naming/LongVariable"">
    <priority>2</priority>
    <properties>
      <property name=""maximum"" value=""50""/>
    </properties>
  </rule>
</ruleset>");
        try
        {
            var (code, stdout, stderr) = RunCli(srcFile, "text", $"csharp,{rulesetFile}", "--only", "LongVariable");
            Assert.Equal(0, code);
            Assert.Empty(stderr);
            Assert.Empty(stdout);
        }
        finally
        {
            File.Delete(srcFile);
            File.Delete(rulesetFile);
        }
    }


    private class FakeRunner : MessSharp.Runner.IRunner
    {
        public MessSharp.Runner.RunOptions? LastOpts { get; private set; }
        public MessSharp.Report.Report FakeReport { get; set; } = new();

        public MessSharp.Report.Report Run(MessSharp.Runner.RunOptions opts)
        {
            LastOpts = opts;
            return FakeReport;
        }
    }

    [Fact]
    public void Cli_WithFakeRunner_PassesCorrectOptions()
    {
        var runner = new FakeRunner();
        var outW = new StringWriter();
        var errW = new StringWriter();
        var args = new[]
        {
            "somepath,otherpath", "text", "codesize",
            "--exclude", "foo,bar",
            "--ignore-tests",
            "--suffixes", "cs,xaml"
        };

        int code = CliRunner.Run(args, outW, errW, runner);

        Assert.Equal(0, code);
        Assert.NotNull(runner.LastOpts);
        Assert.Equal(2, runner.LastOpts.Paths.Count);
        Assert.Contains("somepath", runner.LastOpts.Paths);
        Assert.Contains("otherpath", runner.LastOpts.Paths);
        Assert.Equal(2, runner.LastOpts.Exclude.Count);
        Assert.Contains("foo", runner.LastOpts.Exclude);
        Assert.Contains("bar", runner.LastOpts.Exclude);
        Assert.True(runner.LastOpts.IgnoreTests);
        Assert.Equal(2, runner.LastOpts.Suffixes.Count);
        Assert.Contains(".cs", runner.LastOpts.Suffixes);
        Assert.Contains(".xaml", runner.LastOpts.Suffixes);
    }

    [Fact]
    public void Cli_WithFakeRunner_ReturnsExitViolationWhenViolationsExist()
    {
        var runner = new FakeRunner();
        runner.FakeReport.Violations.Add(new MessSharp.Rule.Violation
        {
            Rule = new DummyRule("SomeRule"),
            File = "file.cs",
            BeginLine = 1,
            Description = "Violation description"
        });

        var outW = new StringWriter();
        var errW = new StringWriter();
        var args = new[] { "somepath", "text", "codesize" };

        int code = CliRunner.Run(args, outW, errW, runner);
        Assert.Equal(2, code);
    }

    [Fact]
    public void Cli_WithFakeRunner_ReturnsExitErrorWhenErrorsExist()
    {
        var runner = new FakeRunner();
        runner.FakeReport.Errors.Add(new MessSharp.Report.ProcessingError
        {
            File = "file.cs",
            Message = "Parse error"
        });

        var outW = new StringWriter();
        var errW = new StringWriter();
        var args = new[] { "somepath", "text", "codesize" };

        int code = CliRunner.Run(args, outW, errW, runner);
        Assert.Equal(1, code);
    }

    [Fact]
    public void Cli_WithColorFlagAndTextFormat_RendersAnsiColors()
    {
        var runner = new FakeRunner();
        runner.FakeReport.Violations.Add(new MessSharp.Rule.Violation
        {
            File = "file.cs",
            BeginLine = 10,
            Rule = new DummyRule("SomeRule"),
            Description = "A violation"
        });

        var outW = new StringWriter();
        var errW = new StringWriter();
        var args = new[] { "somepath", "text", "codesize", "--color" };

        int code = CliRunner.Run(args, outW, errW, runner);
        Assert.Equal(2, code);
        Assert.Contains("\x1b[33m", outW.ToString());
    }

    [Fact]
    public void Cli_WithStrictFlag_PassesStrictToRunner()
    {
        var runner = new FakeRunner();
        var outW = new StringWriter();
        var errW = new StringWriter();
        var args = new[] { "somepath", "text", "codesize", "--strict" };

        int code = CliRunner.Run(args, outW, errW, runner);

        Assert.Equal(0, code);
        Assert.NotNull(runner.LastOpts);
        Assert.True(runner.LastOpts.Strict);
    }

    // End-to-end over the real runner: a syntactically invalid file must be
    // reported as a processing error and drive exit code 1 (#75).
    [Fact]
    public void Cli_SyntaxInvalidFile_ReportsErrorsAndExitsWithError()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-cli-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Broken.cs"),
                "public class Broken\n{\n    public void Method(\n}");

            var (code, stdout, _) = RunCli(directory.FullName, "json", "codesize");

            Assert.Equal(1, code);
            Assert.Contains("errors", stdout);
            Assert.Contains("Broken.cs", stdout);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void Cli_SyntaxInvalidFile_WithIgnoreErrorsOnExit_ExitsCleanButListsErrors()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-cli-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Broken.cs"),
                "public class Broken\n{\n    public void Method(\n}");

            var (code, stdout, _) = RunCli(directory.FullName, "json", "codesize", "--ignore-errors-on-exit");

            Assert.Equal(0, code);
            Assert.Contains("errors", stdout);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Theory]
    [InlineData("--reportfile")]
    [InlineData("--suffixes")]
    [InlineData("--exclude")]
    [InlineData("--enable")]
    [InlineData("--only")]
    [InlineData("--disable")]
    [InlineData("--minimumpriority")]
    [InlineData("--maximumpriority")]
    public void TrailingValueOption_WithoutValue_ReturnsError(string flag)
    {
        var (code, stdout, stderr) = RunCli("somepath", "text", "csharp", flag);
        Assert.Equal(1, code);
        Assert.Contains(flag, stderr);
        Assert.Equal("", stdout);
    }

    [Theory]
    [InlineData("--reportfile")]
    [InlineData("--suffixes")]
    [InlineData("--exclude")]
    [InlineData("--only")]
    [InlineData("--disable")]
    public void ValueOption_FollowedByAnotherOption_ReturnsError(string flag)
    {
        var (code, _, stderr) = RunCli("somepath", "text", "csharp", flag, "--verbose");
        Assert.Equal(1, code);
        Assert.Contains(flag, stderr);
    }

    [Fact]
    public void ExtraPositional_ReturnsError()
    {
        var (code, stdout, stderr) = RunCli("somepath", "text", "csharp", "unexpected");
        Assert.Equal(1, code);
        Assert.Contains("unexpected", stderr);
        Assert.Equal("", stdout);
    }

    [Fact]
    public void ValueOption_WithValue_IsAccepted()
    {
        var dir = Directory.CreateTempSubdirectory("messharp-cli");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "Clean.cs"), "class C { }");
            var (code, _, stderr) = RunCli(dir.FullName, "text", "codesize",
                "--suffixes", "cs", "--minimumpriority", "1", "--exclude", "nothing");
            Assert.Equal(0, code);
            Assert.Equal("", stderr);
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public void Cli_ReportFile_CreatesMissingParentDirectories()
    {
        var dir = Directory.CreateTempSubdirectory("messharp-cli-");
        try
        {
            var sourceFile = Path.Combine(dir.FullName, "Clean.cs");
            File.WriteAllText(sourceFile, "public class Clean { }");
            var reportFile = Path.Combine(dir.FullName, "nested", "parents", "report.json");

            var (code, _, stderr) = RunCli(sourceFile, "json", "csharp", "--reportfile", reportFile);

            Assert.Equal(0, code);
            Assert.Equal("", stderr);
            Assert.True(File.Exists(reportFile));
            Assert.Contains("messharp", File.ReadAllText(reportFile));
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    private class DummyRule : MessSharp.Rule.BaseRule
    {
        public DummyRule(string name)
        {
            Name = name;
            Message = name;
            Priority = 3;
        }
    }
}
