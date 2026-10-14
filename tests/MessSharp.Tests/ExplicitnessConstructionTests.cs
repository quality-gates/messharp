using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessConstructionTests
{
    private static List<(string, string, int)> Analyze(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Select(f => (f.Method, f.Rule.Name, f.BeginLine)).Distinct()
            .OrderBy(f => f.BeginLine).ThenBy(f => f.Method, StringComparer.Ordinal)
            .ThenBy(f => f.Name, StringComparer.Ordinal).ToList();

    [Theory]
    [InlineData("Console.Read();", true)]
    [InlineData("", false)]
    public void ImplicitConstructorsRunTheBaseChain(string statement, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Root {
                public Root() {
                    {{statement}}
                }
            }
            class Mid : Root {
            }
            class Leaf : Mid {
            }
            class Probe {
                static int Run() {
                    var leaf = new Leaf();
                    return 7;
                }
            }
            """);
        Assert.Equal(input ? [("Root", "ImplicitInput", 4), ("Run", "ImplicitInput", 13)] : [], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void ImplicitConstructorsRunFieldInitializers(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Cell {
                public int V = {{value}};
            }
            class Probe {
                static int Run() {
                    return new Cell().V;
                }
            }
            """);
        Assert.Equal(input ? [("Cell", "ImplicitInput", 3), ("Run", "ImplicitInput", 7)] : [], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void ImplicitConstructorsRunInheritedFieldInitializers(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Root {
                public int V = {{value}};
            }
            class Leaf : Root {
            }
            class Probe {
                static int Run() {
                    return new Leaf().V;
                }
            }
            """);
        Assert.Equal(input ? [("Root", "ImplicitInput", 3), ("Run", "ImplicitInput", 9)] : [], findings);
    }

    [Fact]
    public void ImplicitConstructorOfAnUnknownBaseStaysUnresolved()
    {
        var findings = Analyze("""
            using System;
            class Leaf : External {
            }
            class Probe {
                static int Run() {
                    var leaf = new Leaf();
                    return 7;
                }
            }
            """);
        Assert.Equal([("Run", "UnknownEffect", 6)], findings);
    }

    [Fact]
    public void ConstructorInitializersAnchorTheChainEdge()
    {
        var findings = Analyze("""
            using System;
            class Root {
                public Root() {
                    Console.Read();
                }
            }
            class Mid : Root {
                public Mid(int x)
                    : base() {
                }
            }
            class Leaf : Mid {
                public Leaf()
                    : base(1) {
                }
            }
            class Probe {
                static int Run() {
                    var leaf = new Leaf();
                    return 7;
                }
            }
            """);
        Assert.Equal([("Root", "ImplicitInput", 4), ("Mid", "ImplicitInput", 9), ("Leaf", "ImplicitInput", 14),
            ("Run", "ImplicitInput", 19)], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void OverloadedConstructorsShareOneOwnerLine(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Cell {
                public int V = {{value}};
                public Cell(int x)
                    : this() {
                }
                public Cell() {
                }
            }
            class Probe {
                static int Run() {
                    return new Cell(1).V;
                }
            }
            """);
        Assert.Equal(input ? [("Cell", "ImplicitInput", 3), ("Run", "ImplicitInput", 12)] : [], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("Console.Read() + 1", true)]
    [InlineData("-Console.Read()", true)]
    [InlineData("Math.Max(Console.Read(), 0)", true)]
    [InlineData("7", false)]
    public void LazyPresetValuesAreNotFactories(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run() {
                    var lazy = new Lazy<int>({{value}});
                    return lazy.Value;
                }
            }
            """);
        Assert.Equal(input ? [("Run", "ImplicitInput", 4)] : [], findings);
    }

    [Theory]
    [InlineData("Lazy<int>", "Make()", 12)]
    [InlineData("Lazy<int>", "int.Parse(\"7\") + Make()", 12)]
    [InlineData("Lazy<string>", "\"x\" + Make()", 12)]
    [InlineData("Lazy<string>", "$\"{Make()}\"", 12)]
    [InlineData("Lazy<int>", "Maker()", 13)]
    [InlineData("Lazy<int>", "() => Make()", 13)]
    [InlineData("Lazy<int>", "Make", 13)]
    public void LazyArgumentShapeDecidesWhenTheValueIsComputed(string lazy, string argument, int line)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Make() {
                    return Console.Read();
                }
                static Func<int> Maker() {
                    return Make;
                }
                static void Use(object value) {
                }
                static void Run() {
                    var lazy = new {{lazy}}({{argument}});
                    Use(lazy.Value);
                }
            }
            """);
        var expected = new List<(string, string, int)> { ("Make", "ImplicitInput", 4) };
        if (argument.Contains("=>")) expected.Add(("<lazy>", "ImplicitInput", 12));
        expected.Add(("Run", "ImplicitInput", line));
        Assert.Equal(expected, findings);
    }

    [Theory]
    [InlineData("s.Contains(\"q\")", 12)]
    [InlineData("s == \"q\"", 12)]
    [InlineData("!bool.Parse(s)", 12)]
    [InlineData("true", 12)]
    [InlineData("File.Exists(s)", 11)]
    public void LazyBooleanArgumentsSelectTheThreadSafetyOverload(string argument, int line)
    {
        var findings = Analyze($$"""
            using System;
            class Widget {
                public Widget() {
                    Console.Read();
                }
            }
            class Probe {
                static void Use(object value) {
                }
                static void Run(string s) {
                    var lazy = new Lazy<Widget>({{argument}});
                    Use(lazy.Value);
                }
            }
            """);
        Assert.Equal([("Widget", "ImplicitInput", 4), ("Run", "ImplicitInput", line)], findings);
    }

    [Theory]
    [InlineData("s.Substring(1)", false)]
    [InlineData("File.ReadAllText(s)", true)]
    [InlineData("s + \"!\"", false)]
    public void LazyStringValuesArePreset(string argument, bool input)
    {
        var findings = Analyze($$"""
            using System;
            using System.IO;
            class Probe {
                static void Use(object value) {
                }
                static void Run(string s) {
                    var lazy = new Lazy<string>({{argument}});
                    Use(lazy.Value);
                }
            }
            """);
        Assert.Equal(input ? [("Run", "ImplicitInput", 7)] : [], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void LazyPresetDelegateInvokesTargetAtCallLine(string body, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Read() => {{body}};
                static int Run() {
                    Func<int> f = Read;
                    var lazy = new Lazy<Func<int>>(f);
                    return lazy.Value();
                }
            }
            """);
        Assert.Equal(input ? [("Read", "ImplicitInput", 3), ("Run", "ImplicitInput", 7)] : [], findings);
    }
}
