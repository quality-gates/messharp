using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessLazyResolutionTests
{
    private static List<Violation> Analyze(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"));

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void PartiallyKnownLazyValuesRetainUnresolvedExecution(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run(bool pick, Lazy<int> external) {
                    var lazy = pick ? new Lazy<int>(() => {{value}}) : external;
                    return lazy.Value;
                }
            }
            """);
        var expected = input
            ? new[] { ("<lazy>", "ImplicitInput", 4), ("Run", "ImplicitInput", 5), ("Run", "UnknownEffect", 5) }
            : new[] { ("Run", "UnknownEffect", 5) };
        Assert.Equal(expected, findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine))
            .OrderBy(f => f.BeginLine).ThenBy(f => f.Name, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void UnconditionalCoalescingReplacementRemovesKnownNull(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run() {
                    Lazy<int>? lazy = null;
                    lazy ??= new(() => {{value}});
                    return lazy.Value;
                }
            }
            """);
        var expected = input ? new[] { ("<lazy>", "ImplicitInput", 5), ("Run", "ImplicitInput", 6) } : [];
        Assert.Equal(expected, findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
    }

    [Theory]
    [InlineData("Console.Read()", "7")]
    [InlineData("7", "Console.Read()")]
    public void ReboundLazyFormalsForceOnlyTheReplacement(string replacement, string original)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Force(Lazy<int> lazy) {
                    lazy = new Lazy<int>(() => {{replacement}});
                    return lazy.Value;
                }
                static int Run() => Force(new Lazy<int>(() => {{original}}));
            }
            """);
        var expected = replacement == "7"
            ? new[] { ("<lazy>", "ImplicitInput", 7) }
            : new[] { ("<lazy>", "ImplicitInput", 4), ("Force", "ImplicitInput", 5), ("Run", "ImplicitInput", 7) };
        Assert.Equal(expected, findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine))
            .OrderBy(f => f.BeginLine).ThenBy(f => f.Method, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("Console.Read()", "7", 4)]
    [InlineData("7", "Console.Read()", 8)]
    public void CapturedRebindingKeepsLazyFormalExecutionUnresolved(string replacement, string original, int factoryLine)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Force(Lazy<int> lazy) {
                    Action rebind = () => lazy = new Lazy<int>(() => {{replacement}});
                    rebind();
                    return lazy.Value;
                }
                static int Run() => Force(new Lazy<int>(() => {{original}}));
            }
            """);
        Assert.Contains(findings, finding => finding.Method == "<lazy>" && finding.Rule.Name == "ImplicitInput"
            && finding.BeginLine == factoryLine);
        Assert.Contains(findings, finding => finding.Method == "Force" && finding.Rule.Name == "UnknownEffect"
            && finding.BeginLine == 5
            && finding.Description == "The method Force() has an unknown effect: unresolved deferred execution.");
        var caller = findings.Where(finding => finding.Method == "Run").ToList();
        Assert.Contains(caller, finding => finding.Rule.Name == "UnknownEffect" && finding.BeginLine == 8
            && finding.Description == "The method Run() has an unknown effect: unresolved deferred execution.");
        Assert.Equal(replacement != "7", caller.Any(finding => finding.Rule.Name == "ImplicitInput"));
    }
}
