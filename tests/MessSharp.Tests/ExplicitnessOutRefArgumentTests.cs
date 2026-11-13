using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessOutRefArgumentTests
{
    private static List<(string Rule, int Line)> Run(string members) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", "using System;\nclass Probe {\n static int Store;\n"
            + " static int Read() { return Console.Read(); }\n static int Seven() { return 7; }\n" + members + "\n}"),
            new Loader().Load("explicitness")).Where(f => f.Method == "Run").Select(f => (f.Rule.Name, f.BeginLine)).ToList();

    [Theory]
    [InlineData("Read", true)]
    [InlineData("Seven", false)]
    public void OutVarKeepsTheLazyTheCalleeStores(string factory, bool input)
    {
        var findings = Run($"static void Make(out Lazy<int> cell) {{ cell = new Lazy<int>({factory}); }}\n"
            + "static int Run() {\nMake(out var cell);\nreturn cell.Value;\n}");
        Assert.Equal(input ? [("ImplicitInput", 9)] : [], findings);
    }

    [Fact]
    public void TryPatternOutVarKeepsTheLazyTheCalleeStores()
    {
        var findings = Run("static bool TryMake(out Lazy<int> cell) { cell = new Lazy<int>(Read); return true; }\n"
            + "static int Run() {\nif (TryMake(out var cell)) {\nreturn cell.Value;\n}\nreturn 0;\n}");
        Assert.Equal([("ImplicitInput", 9)], findings);
    }

    [Fact]
    public void RefArgumentAlwaysOverwrittenLosesTheOldLazy()
    {
        var findings = Run("static void Swap(ref Lazy<int> cell) { cell = new Lazy<int>(Seven); }\n"
            + "static int Run() {\nvar cell = new Lazy<int>(Read);\nSwap(ref cell);\nreturn cell.Value;\n}");
        Assert.Empty(findings);
    }

    [Fact]
    public void RefArgumentSometimesOverwrittenKeepsTheOldLazy()
    {
        var findings = Run("static void Swap(ref Lazy<int> cell, bool go) { if (go) { cell = new Lazy<int>(Seven); } }\n"
            + "static int Run() {\nvar cell = new Lazy<int>(Read);\nSwap(ref cell, true);\nreturn cell.Value;\n}");
        Assert.Equal([("ImplicitInput", 10)], findings);
    }

    [Fact]
    public void OutThroughARefLocalAliasOnlyWritesTheStatic()
    {
        var findings = Run("static void Fill(out int slot) { slot = 7; }\n"
            + "static int Run() {\nref int alias = ref Store;\nFill(out alias);\nreturn 0;\n}");
        Assert.Equal([("ImplicitOutput", 9)], findings);
    }

    [Theory]
    [InlineData("Pick(seven)", false)]
    [InlineData("Pick(in seven)", true)]
    public void InOverloadIsChosenOnlyWhenTheCallMarksTheArgument(string call, bool input)
    {
        var findings = Run("static int Pick(int value) { return value; }\n"
            + "static int Pick(in int value) { return Console.Read() + value; }\n"
            + $"static int Run() {{\nint seven = 7;\nreturn {call};\n}}");
        Assert.Equal(input ? [("ImplicitInput", 10)] : [], findings);
    }
}
