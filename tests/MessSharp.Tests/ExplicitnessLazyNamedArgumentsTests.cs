using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessLazyNamedArgumentsTests
{
    private static List<Violation> Analyze(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"));

    [Theory]
    [InlineData("", "isThreadSafe: true", false)]
    [InlineData("Lazy<int>", "isThreadSafe: true", false)]
    [InlineData("", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication", false)]
    [InlineData("Lazy<int>", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication", false)]
    [InlineData("", "isThreadSafe: true", true)]
    [InlineData("Lazy<int>", "isThreadSafe: true", true)]
    [InlineData("", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication", true)]
    [InlineData("Lazy<int>", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication", true)]
    public void NamedLazyFactoriesKeepCreationQuietAndTransferAtForce(string type, string option, bool force)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Run() {
                    Lazy<int> lazy = new {{type}}({{option}}, valueFactory: () => Console.Read());
                    return {{(force ? "lazy.Value" : "7")}};
                }
            }
            """);
        var expected = force
            ? new[] { ("<lazy>", "ImplicitInput", 4), ("Run", "ImplicitInput", 5) }
            : new[] { ("<lazy>", "ImplicitInput", 4) };
        Assert.Equal(expected, findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
        Assert.All(findings, f => Assert.Equal($"The method {f.Method}() has an implicit input: uses Console.Read.", f.Description));
    }

    [Theory]
    [InlineData("", "isThreadSafe: true")]
    [InlineData("Lazy<int>", "isThreadSafe: true")]
    [InlineData("", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication")]
    [InlineData("Lazy<int>", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication")]
    public void NamedPureLazyFactoriesRemainQuietAtForce(string type, string option)
    {
        Assert.Empty(Analyze($$"""
            using System;
            class Probe {
                static int Run() {
                    Lazy<int> lazy = new {{type}}({{option}}, valueFactory: () => 7);
                    return lazy.Value;
                }
            }
            """));
    }

    [Theory]
    [InlineData("", "isThreadSafe: true")]
    [InlineData("Lazy<int>", "isThreadSafe: true")]
    [InlineData("", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication")]
    [InlineData("Lazy<int>", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication")]
    public void NamedLazyMethodGroupsTransferAtForce(string type, string option)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static int Read() => Console.Read();
                static int Run() {
                    Lazy<int> lazy = new {{type}}({{option}}, valueFactory: Read);
                    return lazy.Value;
                }
            }
            """);
        Assert.Equal(new[] { ("Read", "ImplicitInput", 3), ("Run", "ImplicitInput", 6) },
            findings.Select(f => (f.Method, f.Rule.Name, f.BeginLine)).OrderBy(f => f.BeginLine));
    }

    [Theory]
    [InlineData("", "isThreadSafe: true", true)]
    [InlineData("Lazy<int>", "isThreadSafe: true", true)]
    [InlineData("", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication", true)]
    [InlineData("Lazy<int>", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication", true)]
    [InlineData("", "isThreadSafe: true", false)]
    [InlineData("Lazy<int>", "isThreadSafe: true", false)]
    [InlineData("", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication", false)]
    [InlineData("Lazy<int>", "mode: System.Threading.LazyThreadSafetyMode.ExecutionAndPublication", false)]
    public void NamedLazyFactoriesUpdateCapturedStorageBeforeLaterMutation(string type, string option, bool callerVisible)
    {
        var findings = Analyze($$"""
            using System;
            class Probe {
                static void Run(int[] values) {
                    var holder = new[] { new int[1] };
                    Lazy<int> lazy = new {{type}}({{option}}, valueFactory: () => { holder[0] = {{(callerVisible ? "values" : "new int[1]")}}; return 7; });
                    _ = lazy.Value;
                    holder[0][0] = 1;
                }
            }
            """);
        var run = findings.Where(f => f.Method == "Run").ToList();
        if (!callerVisible)
        {
            Assert.Empty(run);
            return;
        }
        var output = Assert.Single(run);
        Assert.Equal("ImplicitOutput", output.Rule.Name);
        Assert.Equal(7, output.BeginLine);
        Assert.Equal("The method Run() has an implicit output: changes argument 'values'.", output.Description);
    }
}
