using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessIteratorFactoryTests
{
    private static List<(string Rule, int Line)> Run(string body) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", """
            using System;
            using System.Collections.Generic;
            class Probe {
                static int Read() { return Console.Read(); }
                static int Seven() { return 7; }
                static IEnumerable<int> Gen() { yield return Read(); }
                static IEnumerable<int> Gen7() { yield return Seven(); }
            """ + "\n" + body + "\n}"), new Loader().Load("explicitness"))
            .Where(finding => finding.Method == "Run").Select(f => (f.Rule.Name, f.BeginLine)).ToList();

    private const string Loop = "int total = 0;\nforeach (var x in it) {\ntotal += x;\n}\nreturn total;\n}";

    [Theory]
    [InlineData("Gen", true)]
    [InlineData("Gen7", false)]
    public void LazyOfIteratorRunsWhereTheSequenceIsIterated(string factory, bool input)
    {
        var findings = Run($"static int Run() {{\nvar cell = new Lazy<IEnumerable<int>>({factory});\nvar it = cell.Value;\n{Loop}");
        Assert.Equal(input ? [("ImplicitInput", 12)] : [], findings);
    }

    [Fact]
    public void LazyOfIteratorThroughALambdaRunsWhereTheSequenceIsIterated()
    {
        var findings = Run($"static int Run() {{\nvar cell = new Lazy<IEnumerable<int>>(() => Gen());\nvar it = cell.Value;\n{Loop}");
        Assert.Equal([("ImplicitInput", 12)], findings);
    }

    [Fact]
    public void ForcingALazyIteratorWithoutIteratingIsPure()
    {
        Assert.Empty(Run("static int Run() {\nvar cell = new Lazy<IEnumerable<int>>(Gen);\nvar it = cell.Value;\nreturn 1;\n}"));
    }

    [Fact]
    public void DelegateReturningAnIteratorRunsWhereTheSequenceIsIterated()
    {
        var findings = Run($"static int Run() {{\nFunc<IEnumerable<int>> f = Gen;\nvar it = f();\n{Loop}");
        Assert.Equal([("ImplicitInput", 12)], findings);
    }
}
