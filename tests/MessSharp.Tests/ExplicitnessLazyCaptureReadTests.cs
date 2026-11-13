using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

/// <summary>A closure reads a captured Lazy as the enclosing body holds it where it calls the closure (#241).</summary>
public class ExplicitnessLazyCaptureReadTests
{
    private static List<string> Run(string initial, string rebound, string declaration, string call, string extra = "")
    {
        var source = $$"""
            using System;
            class Probe {
                static int Read() { return Console.Read(); }
                static int Seven() { return 7; }
                static void Apply(Func<int> f) { f(); }
                static int Run() {
                    var cell = new Lazy<int>({{initial}});
                    {{declaration}}
                    cell = new Lazy<int>({{rebound}});
                    {{extra}}
                    return {{call}};
                }
            }
            """;
        return Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Where(f => f.Method == "Run").Select(f => f.Rule.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    private const string Lambda = "Func<int> get = () => cell.Value;";
    private const string Local = "int Get() { return cell.Value; }";

    [Theory]
    [InlineData("Read", "Seven", Lambda, "get()", new string[0])]
    [InlineData("Read", "Seven", Local, "Get()", new string[0])]
    [InlineData("Seven", "Read", Lambda, "get()", new[] { "ImplicitInput" })]
    [InlineData("Seven", "Read", Local, "Get()", new[] { "ImplicitInput" })]
    public void CapturedLazyIsReadAsHeldAtTheCall(string initial, string rebound, string declaration, string call, string[] expected) =>
        Assert.Equal(expected, Run(initial, rebound, declaration, call));

    [Fact]
    public void ClosureUsedOtherThanByDirectCallKeepsEveryValue() =>
        Assert.Contains("ImplicitInput", Run("Read", "Seven", Lambda, "get()", "Apply(get);"));
}
