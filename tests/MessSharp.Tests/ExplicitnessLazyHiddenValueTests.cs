using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

/// <summary>A Lazy subclass that hides Value, and a Value property pattern on a Lazy (#239).</summary>
public class ExplicitnessLazyHiddenValueTests
{
    private static List<string> Run(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Where(f => f.Method == "Run").Select(f => f.Rule.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();

    private const string Probe = """
        class Probe {
            static int Read() { return Console.Read(); }
            static int Seven() { return 7; }
            static int Run() {
                BODY
            }
        }
        """;

    private static string With(string types, string body) =>
        "using System;\n" + types + "\n" + Probe.Replace("BODY", body);

    [Theory]
    [InlineData("Console.Read() + 1")]
    [InlineData("42")]
    public void PartialHidingValueIsNotReadAsTheBaseValue(string hidden) => Assert.Contains("UnknownEffect", Run(With($$"""
        partial class Cell : Lazy<int> { public Cell(Func<int> factory) : base(factory) { } }
        partial class Cell { public new int Value => {{hidden}}; }
        """, "var cell = new Cell(Seven); return cell.Value;")));

    [Theory]
    [InlineData("Console.Read() + 1")]
    [InlineData("42")]
    public void InheritedHidingValueIsNotReadAsTheBaseValue(string hidden) => Assert.Contains("UnknownEffect", Run(With($$"""
        class Mid : Lazy<int> { public Mid(Func<int> factory) : base(factory) { } public new int Value => {{hidden}}; }
        class Leaf : Mid { public Leaf(Func<int> factory) : base(factory) { } }
        """, "var leaf = new Leaf(Seven); return leaf.Value;")));

    [Fact]
    public void PartialSubclassWithoutHidingStaysAStandardLazy()
    {
        const string types = """
            partial class Cell : Lazy<int> { public Cell(Func<int> factory) : base(factory) { } }
            partial class Cell { public int Other => 1; }
            """;
        Assert.Empty(Run(With(types, "var cell = new Cell(Seven); return cell.Value;")));
        Assert.Equal(["ImplicitInput"], Run(With(types, "var cell = new Cell(Read); return cell.Value;")));
    }

    [Theory]
    [InlineData("Read", "cell is { Value: > 0 } ? 1 : 0", new[] { "ImplicitInput" })]
    [InlineData("Seven", "cell is { Value: > 0 } ? 1 : 0", new string[0])]
    [InlineData("Read", "cell switch { { Value: > 0 } => 1, _ => 0 }", new[] { "ImplicitInput" })]
    [InlineData("Seven", "cell switch { { Value: > 0 } => 1, _ => 0 }", new string[0])]
    public void ValuePropertyPatternForcesTheLazy(string factory, string test, string[] expected) =>
        Assert.Equal(expected, Run(With("", $"var cell = new Lazy<int>({factory}); return {test};")));

    [Fact]
    public void ValuePatternOnHidingSubclassIsUnknown() => Assert.Contains("UnknownEffect", Run(With(
        "class Cell : Lazy<int> { public Cell(Func<int> factory) : base(factory) { } public new int Value => Console.Read(); }",
        "var cell = new Cell(Seven); return cell is { Value: > 0 } ? 1 : 0;")));

    [Fact]
    public void NestedTypeSharingATopLevelNameIsNotTakenForTheTopLevelOne() => Assert.Contains("UnknownEffect", Run(With("""
        class Cell : Lazy<int> { public Cell(Func<int> factory) : base(factory) { } }
        class Shelf { public class Cell : Lazy<int> { public Cell(Func<int> factory) : base(factory) { } public new int Value => Console.Read(); } }
        """, "var cell = new Shelf.Cell(Seven); return cell.Value;")));
}
