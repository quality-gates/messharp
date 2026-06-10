using Xunit;
using CliRunner = MessCS.Cli.Cli;

namespace MessCS.Tests;

public class CliTests
{
    private static (int code, string stdout, string stderr) RunCli(params string[] args)
    {
        var outW  = new StringWriter();
        var errW  = new StringWriter();
        int code  = CliRunner.Run(args, outW, errW);
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
        Assert.Contains("messcs", stdout);
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
}
