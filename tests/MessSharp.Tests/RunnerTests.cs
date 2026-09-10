using MessSharp.Report;
using MessSharp.Runner;
using Xunit;
using RuleSetType = MessSharp.Rule.RuleSet;
using RunnerType = MessSharp.Runner.Runner;

namespace MessSharp.Tests;

/// <summary>
/// Runner-level tests for how per-file parse outcomes land in the report:
/// recoverable syntax diagnostics become processing errors (#75).
/// </summary>
public class RunnerTests
{
    private const string BrokenSource = @"
public class Broken
{
    public void Method(
}";

    [Fact]
    public void Run_SyntaxInvalidFile_RecordsProcessingErrorsNamingTheFile()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-runner-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Broken.cs"), BrokenSource);

            var report = new RunnerType().Run(new RunOptions
            {
                Paths = new List<string> { directory.FullName },
                RuleSets = new List<RuleSetType> { new() { Name = "empty" } },
            });

            Assert.NotEmpty(report.Errors);
            Assert.All(report.Errors, e =>
            {
                Assert.EndsWith("Broken.cs", e.File);
                Assert.Matches(@"CS\d+: .+ on line \d+", e.Message);
            });
            Assert.Contains(report.Errors, e => e.Message.Contains(") expected"));
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void Run_SyntaxValidFile_ProducesNoProcessingErrors()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-runner-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Valid.cs"), "public class Valid { }");

            var report = new RunnerType().Run(new RunOptions
            {
                Paths = new List<string> { directory.FullName },
                RuleSets = new List<RuleSetType> { new() { Name = "codesize" } },
            });

            Assert.Empty(report.Errors);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }
}