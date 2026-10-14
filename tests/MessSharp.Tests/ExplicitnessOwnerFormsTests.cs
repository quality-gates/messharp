using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

/// <summary>
/// Member forms that own executable bodies: expression-bodied properties and indexers, accessors, events,
/// operators and conversions. Each action has a calculation twin that must stay quiet.
/// </summary>
public class ExplicitnessOwnerFormsTests
{
    private static string[] Observe(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Select(finding => $"{finding.Method} {finding.Rule.Name} {finding.BeginLine}").Order().ToArray();

    [Theory]
    [InlineData("static int Next => Console.Read();", "get_Next ImplicitInput 3")]
    [InlineData("int Next => Console.Read();", "get_Next ImplicitInput 3")]
    [InlineData("int this[int i] => Console.Read();", "get_Item ImplicitInput 3")]
    [InlineData("int this[int i] { get => Console.Read(); set { } }", "get_Item ImplicitInput 3")]
    [InlineData("int this[int i] { get => 7; set => Console.WriteLine(value); }", "set_Item ImplicitOutput 3")]
    [InlineData("event Action E { add => Console.WriteLine(1); remove { } }", "add_E ImplicitOutput 3")]
    [InlineData("static int operator +(Probe a, Probe b) => Console.Read();", "operator + ImplicitInput 3")]
    [InlineData("static explicit operator int(Probe a) => Console.Read();", "operator explicit int ImplicitInput 3")]
    public void MemberFormBodiesOwnTheirEffects(string member, string expected)
    {
        Assert.Equal(new[] { expected }, Observe(Probe(member)));
    }

    [Theory]
    [InlineData("static int Next => 7;")]
    [InlineData("int Next => 7;")]
    [InlineData("int this[int i] => i;")]
    [InlineData("int this[int i] { get => i; set { } }")]
    [InlineData("event Action E { add { } remove { } }")]
    [InlineData("static int operator +(Probe a, Probe b) => 7;")]
    [InlineData("static explicit operator int(Probe a) => 7;")]
    public void PureMemberFormBodiesStayQuiet(string member)
    {
        Assert.Empty(Observe(Probe(member)));
    }

    [Fact]
    public void InstanceFieldInitializersRunInConstruction()
    {
        Assert.Equal(new[] { "Probe ImplicitInput 3" }, Observe(Probe("int x = Console.Read();")));
        Assert.Empty(Observe(Probe("int x = 7;")));
    }

    private static string Probe(string member) => $$"""
        using System;
        class Probe {
         {{member}}
        }
        """;
}
