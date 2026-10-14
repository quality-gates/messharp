using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessDispatchTests
{
    private static IEnumerable<Violation> Run(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Where(finding => finding.Method == "Run");

    [Theory]
    [InlineData("new Noisy()")]
    [InlineData("new()")]
    public void ExplicitAndTargetTypedNewExecuteTheSameConstructor(string creation)
    {
        var findings = Run("class Noisy { public Noisy() { Console.WriteLine(1); } }"
            + " class Probe { static void Run() {\nNoisy value = " + creation + "; } }");
        var output = Assert.Single(findings);
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(2, output.BeginLine);
        Assert.Equal("The method Run() has an implicit output: uses Console.WriteLine.", output.Description);
    }

    [Theory]
    [InlineData("public static Probe operator +(Probe value, int amount) { Console.WriteLine(1); return value; }",
        "static void Run(Probe value) { value += 1; }")]
    [InlineData("public static explicit operator int(Probe value) { return Console.Read(); }",
        "static int Run(Probe value) { return (int)value; }")]
    [InlineData("public int Number { get { return Console.Read(); } }",
        "static int Run(Probe value) { return value?.Number ?? 0; }")]
    [InlineData("public int this[int index] { get { return Console.Read(); } }",
        "static int Run(Probe value) { return value?[0] ?? 0; }")]
    public void UnsupportedCustomDispatchMarksItsExecutionOwnerUnknown(string members, string method)
    {
        var findings = Run("class Probe { " + members + "\n" + method + " }");
        var unknown = Assert.Single(findings);
        Assert.Equal("UnknownEffect", unknown.Rule.Name);
        Assert.Equal(2, unknown.BeginLine);
    }

    [Theory]
    [InlineData("static int Run(int value) { value += 1; return value; }")]
    [InlineData("static int Run(long value) { return (int)value; }")]
    [InlineData("static int Run(List<int> values) { return values?.Count ?? 0; }")]
    [InlineData("static int Run(List<int> values) { return values?[0] ?? 0; }")]
    public void PrimitiveAndTrustedCollectionTwinsRemainQuiet(string method) =>
        Assert.Empty(Run("class Probe { " + method + " }"));

    [Theory]
    [InlineData("new Holder()")]
    [InlineData("new()")]
    public void CompilerGeneratedEmptyConstructorsRemainQuiet(string creation) =>
        Assert.Empty(Run("class Holder { public List<int> Value; } class Probe { static void Run() { Holder value = "
            + creation + "; } }"));

    [Theory]
    [InlineData("class Holder { public int Value = Console.Read(); }", "ImplicitInput")]
    [InlineData("class Holder { public int Value { get; } = Console.Read(); }", "ImplicitInput")]
    [InlineData("class Base { public Base() { Console.WriteLine(1); } } class Holder : Base {}", "ImplicitOutput")]
    [InlineData("class Holder : ExternalBase {}", "UnknownEffect")]
    public void TargetTypedDefaultInitializationTransfersMemberAndBaseEffects(string declaration, string expectedRule)
    {
        var findings = Run(declaration + "\nclass Probe { static void Run() { Holder value = new(); } }");
        var finding = Assert.Single(findings);
        Assert.Equal(expectedRule, finding.Rule.Name);
        Assert.Equal(2, finding.BeginLine);
    }
}
