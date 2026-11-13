using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;
using static MessSharp.Tests.ExplicitnessExpectations;

namespace MessSharp.Tests;

public class ExplicitnessCallableIdentityTests
{
    private static List<Violation> AnalyzeRecords(string source)
    {
        var records = Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"));
        Assert.Empty(records.GroupBy(f => (f.Package, f.Class, f.Method, f.Rule.Name, f.BeginLine, f.Description))
            .Where(group => group.Count() > 1));
        return records.OrderBy(f => f.BeginLine).ThenBy(f => f.Method, StringComparer.Ordinal)
            .ThenBy(f => f.Rule.Name, StringComparer.Ordinal).ToList();
    }

    private static List<(string, string, int, string)> Analyze(string source) =>
        AnalyzeRecords(source).Select(f => (f.Method, f.Rule.Name, f.BeginLine, f.Description)).ToList();

    [Theory]
    [InlineData("foreach (var x in xs)", "Console.Read()", true)]
    [InlineData("foreach (var x in xs)", "8", false)]
    [InlineData("for (var i = 0; i < xs.Length; i++)", "Console.Read()", true)]
    [InlineData("while (total < xs.Length)", "Console.Read()", true)]
    [InlineData("while (total < xs.Length)", "8", false)]
    public void LoopRebindingReachesTheNextIteration(string loop, string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run(int[] xs) {
                    Func<int> f = () => 7;
                    var total = 0;
                    {{loop}} {
                        total += f();
                        f = () => {{value}};
                    }
                    return total;
                }
            }
            """);
        Assert.Equal(input ? [Input("Run", 7), Input("<lambda>", 8)] : [], findings);
    }

    [Theory]
    [InlineData("(f, n) = (() => Console.Read(), 1);", true)]
    [InlineData("(f, n) = (() => 8, 1);", false)]
    [InlineData("(n, f) = (1, () => Console.Read());", true)]
    [InlineData("(n, (m, f)) = (1, (2, () => Console.Read()));", true)]
    [InlineData("(n, (m, f)) = (1, (2, () => 8));", false)]
    public void TupleDeconstructionBindsEachElement(string deconstruction, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run() {
                    Func<int> f = () => 7;
                    int n, m;
                    {{deconstruction}}
                    return f() + n;
                }
            }
            """);
        Assert.Equal(input ? [Input("<lambda>", 6), Input("Run", 7)] : [], findings);
    }

    [Fact]
    public void OpaqueDeconstructionKeepsTheCallUnresolved()
    {
        var findings = Analyze("""
            using System;
            class Probe {
                static int Run((Func<int>, int) pair) {
                    Func<int> f = () => 7;
                    int n;
                    (f, n) = pair;
                    return f() + n;
                }
            }
            """);
        Assert.Equal([Unknown("Run", 7, "unresolved call 'f'")], findings);
    }

    [Theory]
    [InlineData("(Func<int> f, int n) = (() => Console.Read(), 1);", true)]
    [InlineData("(Func<int> f, int n) = (() => 8, 1);", false)]
    [InlineData("var (f, n) = ((Func<int>)(() => Console.Read()), 1);", true)]
    [InlineData("var (f, n) = ((Func<int>)(() => 8), 1);", false)]
    public void DeclaringDeconstructionBindsEachElement(string deconstruction, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run() {
                    {{deconstruction}}
                    return f() + n;
                }
            }
            """);
        Assert.Equal(input ? [Input("<lambda>", 4), Input("Run", 5)] : [], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("8", false)]
    public void SwappingDeconstructionReadsTheElementsFirst(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run() {
                    Func<int> f = () => 7;
                    Func<int> g = () => {{value}};
                    (f, g) = (g, f);
                    return f();
                }
            }
            """);
        Assert.Equal(input ? [Input("<lambda>", 5), Input("Run", 7)] : [], findings);
    }

    [Fact]
    public void SwapResolvesElementsFromBeforeTheAssignment()
    {
        var findings = Analyze("""
            using System;
            class Probe {
                static int Run() {
                    Func<int> f = () => 7;
                    Func<int> g = () => 8;
                    (f, g) = (g, f);
                    return g();
                }
            }
            """);
        Assert.Equal([], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("8", false)]
    public void OutArgumentReplacesTheCallable(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static void Swap(out Func<int> g) {
                    g = () => {{value}};
                }
                static int Run() {
                    Func<int> f = () => 7;
                    Swap(out f);
                    return f();
                }
            }
            """);
        Assert.Equal(input
            ? [Input("<lambda>", 4), Output("Swap", 4, "writes out parameter 'g'"), Input("Run", 9)]
            : [Output("Swap", 4, "writes out parameter 'g'")], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("8", false)]
    public void OutVariableDesignationCarriesTheCallable(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static bool TryMake(out Func<int> f) {
                    f = () => {{value}};
                    return true;
                }
                static int Run() {
                    if (TryMake(out var f)) {
                        return f();
                    }
                    return 0;
                }
            }
            """);
        Assert.Equal(input
            ? [Input("<lambda>", 4), Output("TryMake", 4, "writes out parameter 'f'"), Input("Run", 9)]
            : [Output("TryMake", 4, "writes out parameter 'f'")], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("8", false)]
    public void CapturedCallableSeesLaterRebinding(string value, bool input)
    {
        var findings = AnalyzeRecords($$"""
            using System;
            class Probe {
                static int Read() {
                    return {{value}};
                }
                static int Run() {
                    Func<int> g = () => 7;
                    Func<int> h = () => g();
                    g = Read;
                    return h();
                }
            }
            """);
        var expected = new List<(string, string, int, string)>();
        if (input)
        {
            expected.Add(("Read", "ImplicitInput", 4, "The method Read() has an implicit input: uses Console.Read."));
            expected.Add(("<lambda>", "ImplicitInput", 8, "The method <lambda>() has an implicit input: uses Console.Read."));
        }
        expected.Add(("<lambda>", "ImplicitInput", 8, "The method <lambda>() has an implicit input: reads captured value 'g'."));
        if (input) expected.Add(("Run", "ImplicitInput", 10, "The method Run() has an implicit input: uses Console.Read."));
        Assert.Equal(expected, findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine, f.Description)));
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("8", false)]
    public void RefLocalWritesTheAliasedCallable(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run() {
                    Func<int> f = () => 7;
                    ref Func<int> r = ref f;
                    r = () => {{value}};
                    return f();
                }
            }
            """);
        Assert.Equal(input ? [Input("<lambda>", 6), Input("Run", 7)] : [], findings);
    }

    [Theory]
    [InlineData("ref var r = ref f;", "r", "Console.Read()", true)]
    [InlineData("ref var r = ref f;", "r", "8", false)]
    [InlineData("ref readonly Func<int> r = ref f;", "r", "Console.Read()", true)]
    [InlineData("ref var r = ref f; ref var s = ref r;", "s", "Console.Read()", true)]
    [InlineData("ref var r = ref f; ref var s = ref r;", "s", "8", false)]
    public void RefLocalReadsTheAliasedCallable(string aliases, string call, string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run() {
                    Func<int> f = () => 7;
                    {{aliases}}
                    f = () => {{value}};
                    return {{call}}();
                }
            }
            """);
        Assert.Equal(input ? [Input("<lambda>", 6), Input("Run", 7)] : [], findings);
    }

    [Fact]
    public void RepointedRefLocalStaysUnresolved()
    {
        var findings = Analyze("""
            using System;
            class Probe {
                static int Run(bool pick) {
                    Func<int> f = () => 7;
                    Func<int> g = () => 8;
                    ref var r = ref f;
                    if (pick) r = ref g;
                    r = () => 9;
                    return f();
                }
            }
            """);
        Assert.Equal([Unknown("Run", 6, "unresolved reference location")], findings);
    }

    [Theory]
    [InlineData("f.Invoke()", "Console.Read()", true)]
    [InlineData("f.Invoke()", "8", false)]
    [InlineData("f?.Invoke() ?? 0", "Console.Read()", true)]
    [InlineData("f?.Invoke() ?? 0", "8", false)]
    [InlineData("f()", "Console.Read()", true)]
    public void InvocationFormsResolveALocalFunctionGroup(string call, string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run() {
                    int Read() {
                        return {{value}};
                    }
                    Func<int> f = Read;
                    return {{call}};
                }
            }
            """);
        Assert.Equal(input ? [Input("Read", 5), Input("Run", 8)] : [], findings);
    }

    [Theory]
    [InlineData("Read", true)]
    [InlineData("() => 8", false)]
    public void ConditionalRebindingJoinsKnownCallables(string replacement, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Read() {
                    return Console.Read();
                }
                static int Run(bool pick) {
                    Func<int> f = () => 7;
                    if (pick) {
                        f = {{replacement}};
                    }
                    return f();
                }
            }
            """);
        Assert.Equal(input
            ? [Input("Read", 4), Input("Run", 11)]
            : [Input("Read", 4)], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("8", false)]
    public void CopiedDelegateParameterResolvesPerCaller(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Apply(Func<int> f) {
                    Func<int> g = f;
                    return g();
                }
                static int Run() {
                    return Apply(() => {{value}});
                }
            }
            """);
        Assert.Equal(input
            ? [Unknown("Apply", 5, "unresolved call 'g'"), Input("<lambda>", 8), Input("Run", 8)]
            : [Unknown("Apply", 5, "unresolved call 'g'")], findings);
    }

    [Theory]
    [InlineData("Make", "Console.Read()", true)]
    [InlineData("Make", "7", false)]
    [InlineData("this.Make", "Console.Read()", true)]
    [InlineData("this.Make", "7", false)]
    public void LazyMethodGroupSelectsTheParameterlessOverload(string group, string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                int Make() {
                    return {{value}};
                }
                int Make(int x) {
                    return x;
                }
                int Run() {
                    var lazy = new Lazy<int>({{group}});
                    return lazy.Value;
                }
            }
            """);
        Assert.Equal(input ? [Input("Make", 4), Input("Run", 11)] : [], findings);
    }

    [Theory]
    [InlineData("Read(y: \"s\")", true)]
    [InlineData("Read(x: 1)", false)]
    [InlineData("Read(\"s\")", true)]
    [InlineData("Probe.Read(y: \"s\")", true)]
    [InlineData("Probe.Read(x: 1)", false)]
    public void NamedArgumentsSelectTheOverload(string call, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Read(int x) {
                    return x;
                }
                static int Read(string y) {
                    return Console.Read();
                }
                static int Run() {
                    return {{call}};
                }
            }
            """);
        Assert.Equal(input
            ? [Input("Read", 7), Input("Run", 10)]
            : [Input("Read", 7)], findings);
    }

    [Theory]
    [InlineData("Read(1)", false)]
    [InlineData("Read(1, 2)", true)]
    [InlineData("Read(y: 2, x: 1)", true)]
    public void DefaultedOverloadLosesToTheExactOne(string call, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Read(int x) {
                    return x;
                }
                static int Read(int x, int y = 0) {
                    return Console.Read();
                }
                static int Run() {
                    return {{call}};
                }
            }
            """);
        Assert.Equal(input
            ? [Input("Read", 7), Input("Run", 10)]
            : [Input("Read", 7)], findings);
    }

    [Theory]
    [InlineData("Read(out var v)", true)]
    [InlineData("Read(1)", false)]
    public void ArgumentRefKindSelectsTheOverload(string call, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Read(int x) {
                    return x;
                }
                static int Read(out int x) {
                    x = 0;
                    return Console.Read();
                }
                static int Run() {
                    return {{call}};
                }
            }
            """);
        Assert.Equal(input
            ? [Output("Read", 7, "writes out parameter 'x'"), Input("Read", 8), Input("Run", 11)]
            : [Output("Read", 7, "writes out parameter 'x'"), Input("Read", 8)], findings);
    }

    [Fact]
    public void ConversionRankedOverloadsStayUnresolved()
    {
        var findings = Analyze("""
            using System;
            class Probe {
                static int Read(int x) {
                    return x;
                }
                static int Read(long x) {
                    return Console.Read();
                }
                static int Run() {
                    return Read(1);
                }
            }
            """);
        Assert.Equal([Input("Read", 7), Unknown("Run", 10, "unresolved call 'Read'")], findings);
    }
}
