using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessCaptureDescriptionTests
{
    [Fact]
    public void CapturedCloneIsNotAnArgumentAndDoesNotEscapeItsOwner()
    {
        var findings = Engine.Analyze(ModelBuilder.Parse("test.cs", """
            class Probe {
                static int Run(int[] xs) {
                    var c = (int[])xs.Clone();
                    void Poke() { c[0] = 7; }
                    Poke();
                    return 1;
                }
            }
            """), new Loader().Load("explicitness"));

        var finding = Assert.Single(findings);
        Assert.Equal("Poke", finding.Method);
        Assert.Equal("ImplicitOutput", finding.Rule.Name);
        Assert.Equal(4, finding.BeginLine);
        Assert.Equal("The method Poke() has an implicit output: changes captured storage 'c'.", finding.Description);
    }

    [Fact]
    public void CapturedMutationTransferredThroughLocalCallsKeepsItsDescription()
    {
        var findings = Engine.Analyze(ModelBuilder.Parse("test.cs", """
            class Probe {
                static void Set(ref int item) { item = 1; }
                static void Run(int[] xs) {
                    void Poke() { Set(ref xs[0]); }
                    void Outer() { _ = xs; Poke(); }
                    Outer();
                }
            }
            """), new Loader().Load("explicitness"));

        Assert.Contains(findings, f => f.Method == "Poke" && f.Description ==
            "The method Poke() has an implicit output: changes captured storage 'xs'.");
        Assert.Contains(findings, f => f.Method == "Outer" && f.Description ==
            "The method Outer() has an implicit output: changes captured storage 'xs'.");
        Assert.Contains(findings, f => f.Method == "Run" && f.Description ==
            "The method Run() has an implicit output: changes argument 'xs'.");
    }

    [Fact]
    public void LambdaMutationNamesCapturedStorage()
    {
        var findings = Engine.Analyze(ModelBuilder.Parse("test.cs", """
            using System;
            class Probe {
                static void Run(int[] xs) {
                    Action poke = () => { xs[0] = 1; };
                    poke();
                }
            }
            """), new Loader().Load("explicitness"));

        Assert.Contains(findings, f => f.Method == "<lambda>" && f.Description ==
            "The method <lambda>() has an implicit output: changes captured storage 'xs'.");
        Assert.Contains(findings, f => f.Method == "Run" && f.Description ==
            "The method Run() has an implicit output: changes argument 'xs'.");
    }

    [Fact]
    public void LazyFactoryMutationNamesCapturedStorage()
    {
        var findings = Engine.Analyze(ModelBuilder.Parse("test.cs", """
            using System;
            class Probe {
                static int Run(int[] xs) {
                    var lazy = new Lazy<int>(() => { xs[0] = 1; return 7; });
                    return lazy.Value;
                }
            }
            """), new Loader().Load("explicitness"));

        Assert.Contains(findings, f => f.Method == "<lazy>" && f.Description ==
            "The method <lazy>() has an implicit output: changes captured storage 'xs'.");
        Assert.Contains(findings, f => f.Method == "Run" && f.Description ==
            "The method Run() has an implicit output: changes argument 'xs'.");
    }

    [Fact]
    public void CapturesAndActualParametersHaveDifferentDescriptions()
    {
        var findings = Engine.Analyze(ModelBuilder.Parse("test.cs", """
            class Probe {
                static void Run(int[] xs) {
                    void Poke(int[] items) { xs[0] = 1; items[0] = 2; }
                    Poke(xs);
                }
            }
            """), new Loader().Load("explicitness"));

        var poke = findings.Where(f => f.Method == "Poke").Select(f => f.Description).OrderBy(s => s).ToArray();
        Assert.Equal(new[] {
            "The method Poke() has an implicit output: changes argument 'items'.",
            "The method Poke() has an implicit output: changes captured storage 'xs'.",
        }, poke);
        var run = Assert.Single(findings.Where(f => f.Method == "Run" && f.Rule.Name == "ImplicitOutput"));
        Assert.Equal("The method Run() has an implicit output: changes argument 'xs'.", run.Description);
    }
}
