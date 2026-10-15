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

    [Theory]
    [InlineData("new(() => Console.Read())")]
    [InlineData("new Lazy<int>(() => Console.Read())")]
    public void AssignedLazyValuesTransferTheirFactoryAtValueAccess(string creation)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Force() {
                    Lazy<int> lazy;
                    lazy = {{creation}};
                    return lazy.Value;
                }
            }
            """);
        Assert.Equal(new[] { ("<lazy>", "ImplicitInput", 5), ("Force", "ImplicitInput", 6) },
            findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
    }

    [Theory]
    [InlineData("new(() => Console.Read())")]
    [InlineData("new Lazy<int>(() => Console.Read())")]
    public void ReturnedLazyFactoriesKeepCreationQuiet(string creation)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static Lazy<int> Make() => {{creation}};
                static int Force() => Make().Value;
            }
            """);
        Assert.Equal(new[] { ("<lazy>", "ImplicitInput", 3), ("Force", "ImplicitInput", 4) },
            findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
    }

    [Theory]
    [InlineData("new(() => Console.Read())")]
    [InlineData("new Lazy<int>(() => Console.Read())")]
    public void BlockReturnedLazyFactoriesKeepCreationQuiet(string creation)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static Lazy<int> Make() {
                    return {{creation}};
                }
                static int Force() => Make().Value;
            }
            """);
        Assert.Equal(new[] { ("<lazy>", "ImplicitInput", 4), ("Force", "ImplicitInput", 6) },
            findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
    }

    [Theory]
    [InlineData("new(() => Console.Read())", "lazy")]
    [InlineData("new Lazy<int>(() => Console.Read())", "lazy")]
    [InlineData("new(() => Console.Read())", "Probe.lazy")]
    [InlineData("new Lazy<int>(() => Console.Read())", "Probe.lazy")]
    public void StaticLazyFieldFactoriesHaveOwnersAndTransferAtValueAccess(string creation, string receiver)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                private static readonly Lazy<int> lazy = {{creation}};
                static int Force() => {{receiver}}.Value;
            }
            """);
        Assert.Equal(new[] { ("<lazy>", "ImplicitInput", 3), ("Force", "ImplicitInput", 4) },
            findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
    }

    [Theory]
    [InlineData("new(() => 7)")]
    [InlineData("new Lazy<int>(() => 7)")]
    public void PureAssignedReturnedAndStaticLazyFactoriesRemainQuiet(string creation)
    {
        Assert.Empty(Analyze($$"""
            using System;
            class Probe {
                private static readonly Lazy<int> lazy = {{creation}};
                static Lazy<int> Make() => {{creation}};
                static int Force() {
                    Lazy<int> assigned;
                    assigned = {{creation}};
                    return assigned.Value + Make().Value + lazy.Value;
                }
            }
            """));
    }

    [Theory]
    [InlineData("lazy", false)]
    [InlineData("alias", true)]
    public void LazyRebindingPreservesTheFactoryOfAnEarlierAlias(string receiver, bool forcesInput)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Force() {
                    Lazy<int> lazy = new(() => Console.Read());
                    var alias = lazy;
                    lazy = new(() => 7);
                    return {{receiver}}.Value;
                }
            }
            """);
        var factory = Assert.Single(findings.Where(f => f.Method == "<lazy>"));
        Assert.Equal("ImplicitInput", factory.Rule.Name);
        Assert.Equal(4, factory.BeginLine);
        var forced = findings.Where(f => f.Method == "Force").ToList();
        if (forcesInput)
        {
            var input = Assert.Single(forced);
            Assert.Equal("ImplicitInput", input.Rule.Name);
            Assert.Equal(7, input.BeginLine);
        }
        else Assert.Empty(forced);
    }

    [Theory]
    [InlineData("new(() => { values[0] = 1; return 7; })")]
    [InlineData("new Lazy<int>(() => { values[0] = 1; return 7; })")]
    public void ReturnedLazyFactoriesBindCapturedArgumentsAtTheForce(string creation)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static Lazy<int> Make(int[] values) => {{creation}};
                static int Force(int[] items) => Make(items).Value;
                static int Quiet() => Make(new int[1]).Value;
            }
            """);
        Assert.Equal(new[] { ("<lazy>", "ImplicitOutput", 3), ("Force", "ImplicitOutput", 4) },
            findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
        Assert.Contains(findings, f => f.Method == "Force"
            && f.Description == "The method Force() has an implicit output: changes argument 'items'.");
    }

    [Fact]
    public void StaticLazyFieldsResolveWithinTheirDeclaringClass()
    {
        var findings = Analyze("""
            using System;
            class Noisy {
                private static readonly Lazy<int> value = new(() => Console.Read());
                static int Force() => value.Value;
            }
            class Quiet {
                private static readonly Lazy<int> value = new(() => 7);
                static int Force() => value.Value;
            }
            """);
        Assert.Equal(new[] { ("Noisy", "<lazy>", "ImplicitInput", 3), ("Noisy", "Force", "ImplicitInput", 4) },
            findings.Select(f => (f.Class, f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
    }

    [Theory]
    [InlineData("lazy")]
    [InlineData("Probe.lazy")]
    public void ReplacedStaticLazyFieldsUseTheNewFactory(string reference)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                private static Lazy<int> lazy = new(() => Console.Read());
                static int Force() {
                    {{reference}} = new(() => 7);
                    return {{reference}}.Value;
                }
            }
            """);
        var forced = findings.Where(f => f.Method == "Force").ToList();
        Assert.Contains(forced, f => f.Rule.Name == "ImplicitOutput");
        Assert.DoesNotContain(forced, f => f.Rule.Name == "UnknownEffect" || f.Description.Contains("uses Console.Read"));
    }
}
