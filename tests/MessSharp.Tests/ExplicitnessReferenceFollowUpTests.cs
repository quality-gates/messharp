using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessReferenceFollowUpTests
{
    private static List<Violation> Run(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Where(finding => finding.Method == "Run").ToList();

    private const string Statics = "using System; class Probe { static int count; static void Set(ref int x) { x = 5; } "
        + "static void Keep(ref int x) { } static int Get(ref int x) { return x; } ";

    [Fact]
    public void PassingAStaticByRefToAWriterIsAnOutputWithoutAnInput()
    {
        var output = Assert.Single(Run(Statics + "static int Run() {\nSet(ref count);\nreturn 5; } }"));
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(2, output.BeginLine);
        Assert.Equal("The method Run() has an implicit output: writes static member 'count'.", output.Description);
    }

    [Fact]
    public void PassingAStaticByRefToAReaderIsAnInput()
    {
        var input = Assert.Single(Run(Statics + "static int Run() {\nreturn Get(ref count); } }"));
        Assert.Equal("ImplicitInput", input.Rule.Name);
        Assert.Equal(2, input.BeginLine);
    }

    [Fact]
    public void ReadingThroughARefParameterChainReachesTheStatic()
    {
        var source = Statics + "static int Via(ref int y) { return Get(ref y); } static int Run() {\nreturn Via(ref count); } }";
        Assert.Equal("ImplicitInput", Assert.Single(Run(source)).Rule.Name);
    }

    [Fact]
    public void PassingAStaticByRefToACalleeThatIgnoresItIsQuiet() =>
        Assert.Empty(Run(Statics + "static int Run() { Keep(ref count); return 5; } }"));

    [Fact]
    public void RefLocalAliasingAStaticReportsTheWriteThroughACallee()
    {
        var output = Assert.Single(Run(Statics + "static int Run() {\nref int r = ref count;\nSet(ref r);\nreturn 5; } }"));
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(3, output.BeginLine);
    }

    [Fact]
    public void RefLocalAliasingAStaticStillReportsReadsThroughTheAlias()
    {
        var input = Assert.Single(Run(Statics + "static int Run() {\nref int r = ref count;\nreturn r; } }"));
        Assert.Equal("ImplicitInput", input.Rule.Name);
    }

    [Fact]
    public void RefLocalAliasingAStaticReportsReadsThroughACallee()
    {
        var input = Assert.Single(Run(Statics + "static int Run() {\nref int r = ref count;\nreturn Get(ref r); } }"));
        Assert.Equal("ImplicitInput", input.Rule.Name);
    }

    private const string Swap = "using System; class Probe { static int n; static int Seven() { return 7; } "
        + "static int Bump() { n++; return 1; } ";

    [Fact]
    public void TupleSwapOfDelegateLocalsResolvesThePureTwin() =>
        Assert.Empty(Run(Swap + "static int Run() { Func<int> f = Seven; Func<int> g = Bump; (f, g) = (g, f); return g(); } }"));

    [Fact]
    public void TupleSwapOfDelegateLocalsResolvesTheActionTwin()
    {
        var findings = Run(Swap + "static int Run() {\nFunc<int> f = Seven;\nFunc<int> g = Bump;\n(f, g) = (g, f);\nreturn f(); } }");
        Assert.Contains(findings, finding => finding.Rule.Name == "ImplicitOutput" && finding.BeginLine == 5);
        Assert.DoesNotContain(findings, finding => finding.Rule.Name == "UnknownEffect");
    }

    [Fact]
    public void ThreeWayRotationFollowsEachTarget()
    {
        var findings = Run(Swap + "static int Run() {\nFunc<int> a = Seven;\nFunc<int> b = Bump;\nFunc<int> c = Seven;\n(a, b, c) = (b, c, a);\nreturn a(); } }");
        Assert.Contains(findings, finding => finding.Rule.Name == "ImplicitOutput" && finding.BeginLine == 6);
    }
}
