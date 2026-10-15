using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessCallableIdentityTests
{
    private static List<(string, string, int)> Analyze(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Select(f => (f.Method, f.Rule.Name, f.BeginLine)).Distinct()
            .OrderBy(f => f.BeginLine).ThenBy(f => f.Method, StringComparer.Ordinal)
            .ThenBy(f => f.Name, StringComparer.Ordinal).ToList();

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
        Assert.Equal(input ? [("Run", "ImplicitInput", 7), ("<lambda>", "ImplicitInput", 8)] : [], findings);
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
        Assert.Equal(input ? [("<lambda>", "ImplicitInput", 6), ("Run", "ImplicitInput", 7)] : [], findings);
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
        Assert.Equal([("Run", "UnknownEffect", 7)], findings);
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
        Assert.Equal(input ? [("<lambda>", "ImplicitInput", 4), ("Run", "ImplicitInput", 5)] : [], findings);
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
        Assert.Equal(input ? [("<lambda>", "ImplicitInput", 5), ("Run", "ImplicitInput", 7)] : [], findings);
    }

    [Fact]
    public void ElementReadingAnEarlierTargetStaysUnresolved()
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
        Assert.Equal([("Run", "UnknownEffect", 7)], findings);
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
            ? [("<lambda>", "ImplicitInput", 4), ("Swap", "ImplicitOutput", 4), ("Run", "ImplicitInput", 9)]
            : [("Swap", "ImplicitOutput", 4)], findings);
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
            ? [("<lambda>", "ImplicitInput", 4), ("TryMake", "ImplicitOutput", 4), ("Run", "ImplicitInput", 9)]
            : [("TryMake", "ImplicitOutput", 4)], findings);
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("8", false)]
    public void CapturedCallableSeesLaterRebinding(string value, bool input)
    {
        var findings = Analyze($$"""
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
        Assert.Equal(input
            ? [("Read", "ImplicitInput", 4), ("<lambda>", "ImplicitInput", 8), ("Run", "ImplicitInput", 10)]
            : [("<lambda>", "ImplicitInput", 8)], findings);
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
        Assert.Equal(input ? [("<lambda>", "ImplicitInput", 6), ("Run", "ImplicitInput", 7)] : [], findings);
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
        Assert.Equal(input ? [("<lambda>", "ImplicitInput", 6), ("Run", "ImplicitInput", 7)] : [], findings);
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
        Assert.Equal([("Run", "UnknownEffect", 6)], findings);
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
        Assert.Equal(input ? [("Read", "ImplicitInput", 5), ("Run", "ImplicitInput", 8)] : [], findings);
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
            ? [("Read", "ImplicitInput", 4), ("Run", "ImplicitInput", 11)]
            : [("Read", "ImplicitInput", 4)], findings);
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
            ? [("Apply", "UnknownEffect", 5), ("<lambda>", "ImplicitInput", 8), ("Run", "ImplicitInput", 8)]
            : [("Apply", "UnknownEffect", 5)], findings);
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
        Assert.Equal(input ? [("Make", "ImplicitInput", 4), ("Run", "ImplicitInput", 11)] : [], findings);
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
            ? [("Read", "ImplicitInput", 7), ("Run", "ImplicitInput", 10)]
            : [("Read", "ImplicitInput", 7)], findings);
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
            ? [("Read", "ImplicitInput", 7), ("Run", "ImplicitInput", 10)]
            : [("Read", "ImplicitInput", 7)], findings);
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
            ? [("Read", "ImplicitOutput", 7), ("Read", "ImplicitInput", 8), ("Run", "ImplicitInput", 11)]
            : [("Read", "ImplicitOutput", 7), ("Read", "ImplicitInput", 8)], findings);
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
        Assert.Equal([("Read", "ImplicitInput", 7), ("Run", "UnknownEffect", 10)], findings);
    }
}
