using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

/// <summary>User-defined implicit conversions and operator true/false run code the caller cannot see (#237).</summary>
public class ExplicitnessConversionDispatchTests
{
    private static List<string> Run(string body)
    {
        var source = $$"""
            using System;
            class Celsius { public static implicit operator int(Celsius c) => Console.Read(); }
            class Gate {
                public static bool operator true(Gate g) => Console.Read() > 0;
                public static bool operator false(Gate g) => false;
                public static Gate operator &(Gate a, Gate b) => a;
            }
            class Plain { public int X; }
            class Probe {
                static int Run() {
                    {{body}}
                }
            }
            """;
        return Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Where(f => f.Method == "Run").Select(f => f.Rule.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    [Theory]
    [InlineData("int t = new Celsius(); return t + 1;")]
    [InlineData("int t; t = new Celsius(); return t + 1;")]
    [InlineData("return new Celsius();")]
    [InlineData("var g = new Gate(); if (g) { return 1; } return 0;")]
    [InlineData("var g = new Gate(); while (g) { return 1; } return 0;")]
    [InlineData("var g = new Gate(); return g ? 1 : 0;")]
    [InlineData("var g = new Gate(); var h = new Gate(); return (g && h) ? 1 : 0;")]
    public void UserDefinedConversionAndTruthinessAreNotSilent(string body) =>
        Assert.Contains("UnknownEffect", Run(body));

    [Theory]
    [InlineData("long t = 5; return (int)t;")]
    [InlineData("var p = new Plain(); object o = p; return o == null ? 0 : 1;")]
    [InlineData("bool b = Console.Read() > 0; if (b) { return 1; } return 0;")]
    [InlineData("var c = new Celsius(); var same = c; return same == null ? 0 : 1;")]
    [InlineData("var p = new Plain(); Plain q = p; return q.X;")]
    public void OrdinaryConversionsAndConditionsStayQuietOfUnknown(string body) =>
        Assert.DoesNotContain("UnknownEffect", Run(body));
}
