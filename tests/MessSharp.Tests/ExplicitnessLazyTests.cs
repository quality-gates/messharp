using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessLazyTests
{
    private static List<Violation> Analyze(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"));

    [Theory]
    [InlineData("new(() => Console.Read())")]
    [InlineData("new Lazy<int>(() => Console.Read())")]
    public void LazyFactoryOwnerRemainsSeparateFromQuietCreation(string creation)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Calculate() {
                    Lazy<int> lazy = {{creation}};
                    return 7;
                }
            }
            """);
        var factory = Assert.Single(findings);
        Assert.Equal("Probe", factory.Class);
        Assert.Equal("<lazy>", factory.Method);
        Assert.Equal("ImplicitInput", factory.Rule.Name);
        Assert.Equal(4, factory.BeginLine);
    }

    [Theory]
    [InlineData("new(() => Console.Read())")]
    [InlineData("new Lazy<int>(() => Console.Read())")]
    public void ValueAccessTransfersTheLazyFactoryInputAtTheForcingSite(string creation)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Force() {
                    Lazy<int> lazy = {{creation}};
                    return lazy.Value;
                }
            }
            """);
        Assert.Equal(new[] { ("Probe", "<lazy>", "ImplicitInput", 4), ("Probe", "Force", "ImplicitInput", 5) },
            findings.Select(finding => (finding.Class, finding.Method, finding.Rule.Name, finding.BeginLine))
                .OrderBy(finding => finding.BeginLine));
    }

    [Theory]
    [InlineData("new(() => 7)")]
    [InlineData("new Lazy<int>(() => 7)")]
    public void PureLazyFactoryAndValueAccessRemainQuiet(string creation)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Calculate() {
                    Lazy<int> lazy = {{creation}};
                    return lazy.Value;
                }
            }
            """);
        Assert.Empty(findings);
    }
}
