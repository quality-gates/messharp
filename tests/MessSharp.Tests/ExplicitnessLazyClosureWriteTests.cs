using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

/// <summary>A closure that rebinds a captured Lazy, run only where its enclosing body invokes it (#242).</summary>
public class ExplicitnessLazyClosureWriteTests
{
    private static List<(string Method, string Rule)> Analyze(string initial, string swapped, string statement)
    {
        var source = $$"""
            using System;
            class Probe {
                static int Read() { return Console.Read(); }
                static int Seven() { return 7; }
                static void Apply(Action a) { a(); }
                static int Run(bool flag) {
                    var cell = new Lazy<int>({{initial}});
                    Action swap = () =>
                        cell = new Lazy<int>({{swapped}});
                    {{statement}}
                    return cell.Value;
                }
            }
            """;
        return Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Where(f => f.Method is "Run" or "<lambda>").Select(f => (f.Method, f.Rule.Name))
            .Distinct().OrderBy(f => f.Method, StringComparer.Ordinal).ThenBy(f => f.Name, StringComparer.Ordinal).ToList();
    }

    [Theory]
    [InlineData("Read", "Seven", "swap();", new string[0])]
    [InlineData("Seven", "Read", "swap();", new[] { "ImplicitInput" })]
    [InlineData("Read", "Seven", "", new[] { "ImplicitInput" })]
    [InlineData("Seven", "Read", "", new string[0])]
    [InlineData("Seven", "Read", "if (flag) swap();", new[] { "ImplicitInput" })]
    public void InvokedClosureWriteIsOrderedAtTheInvocation(string initial, string swapped, string statement, string[] run)
    {
        var findings = Analyze(initial, swapped, statement);
        Assert.DoesNotContain(findings, f => f.Rule == "UnknownEffect");
        Assert.Equal(run, findings.Where(f => f.Method == "Run").Select(f => f.Rule));
    }

    [Theory]
    [InlineData("Apply(swap);")]
    [InlineData("Action again = () => swap(); again();")]
    public void EscapedClosureWriteStaysUnknown(string statement) =>
        Assert.Contains(Analyze("Read", "Seven", statement), f => f is ("Run", "UnknownEffect"));
}
