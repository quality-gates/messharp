using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessLazyCallContextTests
{
    private static List<Violation> Analyze(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"));

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void ReturnedLazySwitchArmsRetainFactoryProvenance(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static Lazy<int> Make(bool pick) {
                    return pick switch {
                        true => new(() => {{value}}),
                        _ => new(() => 7)
                    };
                }
                static int Run(bool pick) {
                    return Make(pick).Value;
                }
            }
            """);
        var expected = input ? new[] { ("<lazy>", "ImplicitInput", 5), ("Run", "ImplicitInput", 10) } : [];
        Assert.Equal(expected, findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void TypedDelegateResultsSupplyLazyConstructorContext(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run() {
                    Func<Lazy<int>> make = () => new(() => {{value}});
                    return make().Value;
                }
            }
            """);
        var expected = input ? new[] { ("<lazy>", "ImplicitInput", 4), ("Run", "ImplicitInput", 5) } : [];
        Assert.Equal(expected, findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void KnownLazyActualsResolveDeferredHelperEffects(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Force(Lazy<int> lazy) {
                    return lazy.Value;
                }
                static int Run() {
                    return Force(new Lazy<int>(() => {{value}}));
                }
            }
            """);
        var expected = input
            ? new[] { ("Force", "UnknownEffect", 4), ("<lazy>", "ImplicitInput", 7), ("Run", "ImplicitInput", 7) }
            : new[] { ("Force", "UnknownEffect", 4) };
        Assert.Equal(expected, findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine))
            .OrderBy(f => f.BeginLine).ThenBy(f => f.Method, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void DeferredHelperRequestsFollowRenamedFormalArguments(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Force(Lazy<int> lazy) => lazy.Value;
                static int Forward(Lazy<int> value) => Force(value);
                static int Run() => Forward(new Lazy<int>(() => {{value}}));
            }
            """);
        var expected = input
            ? new[] { ("Force", "UnknownEffect", 3), ("Forward", "UnknownEffect", 4), ("<lazy>", "ImplicitInput", 5), ("Run", "ImplicitInput", 5) }
            : new[] { ("Force", "UnknownEffect", 3), ("Forward", "UnknownEffect", 4) };
        Assert.Equal(expected, findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine))
            .OrderBy(f => f.BeginLine).ThenBy(f => f.Method, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void DeferredHelperRequestsFollowFormalAliases(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Force(Lazy<int> lazy) {
                    var alias = lazy;
                    return alias.Value;
                }
                static int Run() => Force(new Lazy<int>(() => {{value}}));
            }
            """);
        var expected = input
            ? new[] { ("Force", "UnknownEffect", 5), ("<lazy>", "ImplicitInput", 7), ("Run", "ImplicitInput", 7) }
            : new[] { ("Force", "UnknownEffect", 5) };
        Assert.Equal(expected, findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine))
            .OrderBy(f => f.BeginLine).ThenBy(f => f.Method, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("Console.Read()", true)]
    [InlineData("7", false)]
    public void PartiallyKnownLazyArgumentsRetainUnresolvedExecution(string value, bool input)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Force(Lazy<int> lazy) => lazy.Value;
                static int Run(bool pick, Lazy<int> external) {
                    return Force(pick ? new Lazy<int>(() => {{value}}) : external);
                }
            }
            """);
        var expected = input
            ? new[] { ("Force", "UnknownEffect", 3), ("<lazy>", "ImplicitInput", 5), ("Run", "ImplicitInput", 5), ("Run", "UnknownEffect", 5) }
            : new[] { ("Force", "UnknownEffect", 3), ("Run", "UnknownEffect", 5) };
        Assert.Equal(expected, findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine))
            .OrderBy(f => f.BeginLine).ThenBy(f => f.Method, StringComparer.Ordinal).ThenBy(f => f.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void KnownLazyHelperForcesCapturedStorageBeforeLaterMutation()
    {
        var findings = Analyze("""
            using System;
            class Probe {
                static int Force(Lazy<int> lazy) => lazy.Value;
                static void Run(int[] values) {
                    var holder = new[] { new int[1] };
                    var lazy = new Lazy<int>(() => { holder[0] = values; return 7; });
                    _ = Force(lazy);
                    holder[0][0] = 1;
                }
            }
            """);
        var output = Assert.Single(findings.Where(f => f.Method == "Run"));
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(8, output.BeginLine);
        Assert.Equal("The method Run() has an implicit output: changes argument 'values'.", output.Description);
    }
}
