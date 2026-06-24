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
        Assert.Contains("messharp", stdout);
    }

    [Fact]
    public void HelpFlag_PrintsUsage()
    {
        var (code, stdout, _) = RunCli("--help");
        Assert.Equal(0, code);
        Assert.Contains("Usage", stdout);
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
    public void MissingPath_ReturnsError()
    {
        var (code, _, stderr) = RunCli("./does-not-exist-anywhere", "text", "codesize");
        Assert.Equal(1, code);
        Assert.Contains("no such file or directory", stderr);
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
