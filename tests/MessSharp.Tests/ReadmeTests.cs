using System.Text.RegularExpressions;
using CliRunner = MessSharp.Cli.Cli;
using Xunit;

namespace MessSharp.Tests;

public class ReadmeTests
{
    [Fact]
    public void RulesetsSection_ListsEveryBuiltInRuleset()
    {
        string readme = File.ReadAllText(FindRepositoryFile("README.md"));
        string rulesetTable = ExtractRulesetTable(readme);

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = CliRunner.Run(["--help"], stdout, stderr);

        Assert.Equal(0, exitCode);

        string help = stdout.ToString();
        Match match = Regex.Match(help, @"Built-in:\s*(?<rulesets>.+)");
        Assert.True(match.Success, "CLI help should list built-in rulesets.");

        string[] builtInRulesets = match.Groups["rulesets"].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (string ruleset in builtInRulesets)
        {
            Assert.Matches($@"\|\s*(\*\*)?`{Regex.Escape(ruleset)}`(\*\*)?\s*\|", rulesetTable);
        }
    }

    private static string ExtractRulesetTable(string readme)
    {
        const string header = "| Ruleset | What it checks |";
        string[] lines = readme.Split(Environment.NewLine);
        int start = Array.FindIndex(lines, line => line == header);
        Assert.True(start >= 0, "README should contain the ruleset table.");

        var tableLines = lines
            .Skip(start)
            .TakeWhile(line => line.StartsWith('|'))
            .ToArray();

        Assert.NotEmpty(tableLines);

        return string.Join(Environment.NewLine, tableLines);
    }

    private static string FindRepositoryFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {fileName} from {AppContext.BaseDirectory}.");
    }
}
