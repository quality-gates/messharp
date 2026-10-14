using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

/// <summary>
/// Where a forced <c>Lazy&lt;T&gt;</c> comes from: subclasses, out arguments, factory-less constructors,
/// method groups, helpers, static constructors, rebound parameters, collections and member calls.
/// Every action case has a calculation twin that differs only in the factory body.
/// </summary>
public class ExplicitnessLazyProvenanceTests
{
    private static string[] Observe(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"))
            .Select(finding => $"{finding.Method} {finding.Rule.Name} {finding.BeginLine}").Order().ToArray();

    private static void AssertTwins(string action, string[] expected, string[] calculation)
    {
        Assert.Equal(expected.Order().ToArray(), Observe(action));
        Assert.Equal(calculation.Order().ToArray(), Observe(action.Replace("Console.Read()", "7")));
    }

    [Fact]
    public void LazySubclassValueRunsTheFactoryPassedToBase() => AssertTwins("""
        using System;
        class Cell : Lazy<int> {
         public Cell(Func<int> factory) : base(factory) {
         }
        }
        class Probe {
         static readonly Cell cell = new(() => Console.Read());
         static int Run() {
          return cell.Value;
         }
        }
        """, ["<lazy> ImplicitInput 7", "Run ImplicitInput 9"], []);

    [Fact]
    public void LazySubclassWithAnUnknownFactoryFailsClosed()
    {
        var findings = Observe("""
            using System;
            class Cell : Lazy<int> {
             public Cell(Func<int> factory) : base(factory) {
             }
            }
            class Probe {
             static int Run(Cell cell) {
              return cell.Value;
             }
            }
            """);
        Assert.Contains("Run UnknownEffect 8", findings);
    }

    [Fact]
    public void LazyWrittenThroughAnOutArgumentIsForcedByTheCaller() => AssertTwins("""
        using System;
        class Probe {
         static void Make(out Lazy<int> lazy) {
          lazy = new(() => Console.Read());
         }
         static int Run() {
          Make(out Lazy<int> lazy);
          return lazy.Value;
         }
        }
        """, ["<lazy> ImplicitInput 4", "Make ImplicitOutput 4", "Run ImplicitInput 8"], ["Make ImplicitOutput 4"]);

    [Theory]
    [InlineData("new(true)")]
    [InlineData("new(LazyThreadSafetyMode.None)")]
    [InlineData("new Lazy<int>(isThreadSafe: false)")]
    public void FactoryLessLazyValueIsTheDefault(string creation)
    {
        var source = $$"""
            using System;
            using System.Threading;
            class Probe {
             static int Run() {
              Lazy<int> lazy = {{creation}};
              return lazy.Value + Console.Read();
             }
            }
            """;
        Assert.Equal(new[] { "Run ImplicitInput 6" }, Observe(source));
        Assert.Empty(Observe(source.Replace(" + Console.Read()", "")));
    }

    [Fact]
    public void FactoryLessLazyDefersTheValueConstructor() => AssertTwins("""
        using System;
        class Payload {
         public Payload() {
          _ = Console.Read();
         }
        }
        class Probe {
         static Payload Run() {
          var lazy = new Lazy<Payload>();
          return lazy.Value;
         }
         static Lazy<Payload> Create() {
          return new Lazy<Payload>();
         }
        }
        """, ["Payload ImplicitInput 4", "Run ImplicitInput 10"], []);

    [Fact]
    public void ConditionalLazyValueTransfersTheFactory() => AssertTwins("""
        using System;
        class Probe {
         static int Run() {
          Lazy<int>? lazy = new(() => Console.Read());
          return lazy?.Value ?? 0;
         }
        }
        """, ["<lazy> ImplicitInput 4", "Run ImplicitInput 5"], []);

    [Fact]
    public void HiddenLazyValueDoesNotSilencePropertyDispatch()
    {
        var findings = Observe("""
            using System;
            class Cell : Lazy<int> {
             public Cell(Func<int> factory) : base(factory) { }
             public new int Value => Console.Read();
            }
            class Probe {
             static int Run() {
              var cell = new Cell(() => 7);
              return cell.Value;
             }
            }
            """);
        Assert.Contains("get_Value ImplicitInput 4", findings);
        Assert.Contains("Run UnknownEffect 9", findings);
    }

    [Fact]
    public void LocalFunctionGroupFactoryIsResolved() => AssertTwins("""
        using System;
        class Probe {
         static int Run() {
          int Read() {
           return Console.Read();
          }
          Lazy<int> lazy = new(Read);
          return lazy.Value;
         }
        }
        """, ["Read ImplicitInput 5", "Run ImplicitInput 8"], []);

    [Fact]
    public void LazyReturnedThroughAnIdentityHelperKeepsItsFactory() => AssertTwins("""
        using System;
        class Probe {
         static Lazy<int> Same(Lazy<int> lazy) {
          return lazy;
         }
         static int Run() {
          Lazy<int> first = new(() => Console.Read());
          return Same(first).Value;
         }
        }
        """, ["<lazy> ImplicitInput 7", "Run ImplicitInput 8"], []);

    [Fact]
    public void LazyAssignedInAStaticConstructorIsResolved() => AssertTwins("""
        using System;
        class Probe {
         static readonly Lazy<int> cache;
         static Probe() {
          cache = new(() => Console.Read());
         }
         static int Run() {
          return cache.Value;
         }
        }
        """, ["<lazy> ImplicitInput 5", "Run ImplicitInput 8"], []);

    [Fact]
    public void ReboundParameterForcesItsLocalFactory() => AssertTwins("""
        using System;
        class Probe {
         static int Force(Lazy<int> lazy) {
          lazy = new(() => Console.Read());
          return lazy.Value;
         }
         static int Run() {
          return Force(new Lazy<int>(() => 8));
         }
        }
        """, ["<lazy> ImplicitInput 4", "Force ImplicitInput 5", "Run ImplicitInput 8"], []);

    [Fact]
    public void ReboundParameterDoesNotForceTheCallerFactory()
    {
        Assert.Equal(new[] { "<lazy> ImplicitInput 8" }, Observe("""
            using System;
            class Probe {
             static int Force(Lazy<int> lazy) {
              lazy = new(() => 7);
              return lazy.Value;
             }
             static int Run() {
              return Force(new Lazy<int>(() => Console.Read()));
             }
            }
            """));
    }

    [Fact]
    public void LambdaNestedInAFactoryIsALambdaOwner()
    {
        var source = """
            using System;
            class Probe {
             static int Run() {
              Lazy<int> lazy = new(() => {
               Func<int> read = () => Console.Read();
               return read();
              });
              return lazy.Value;
             }
            }
            """;
        Assert.Equal(new[] { "<lambda> ImplicitInput 5", "<lazy> ImplicitInput 6", "Run ImplicitInput 8" }, Observe(source));
        Assert.Equal(new[] { "<lambda> ImplicitInput 5" }, Observe(source.Replace("return read();", "return 7;")));
    }

    [Theory]
    [InlineData("Lazy<int> lazy = given ?? new(() => Console.Read());", "return lazy.Value;")]
    [InlineData("Lazy<int>[] items = new Lazy<int>[] { new(() => Console.Read()) };", "return items[0].Value;")]
    [InlineData("Lazy<int>[] items = [new(() => Console.Read())];", "return items[0].Value;")]
    public void FactoriesInsideCoalesceAndCollectionsAreLazyOwners(string declaration, string force)
    {
        var source = $$"""
            using System;
            class Probe {
             static int Run(Lazy<int>? given) {
              {{declaration}}
              {{force}}
             }
            }
            """;
        var findings = Observe(source);
        Assert.Contains("<lazy> ImplicitInput 4", findings);
        Assert.Contains("Run ImplicitInput 5", findings);
        Assert.DoesNotContain(findings, finding => finding.StartsWith("<lambda>", StringComparison.Ordinal));
        Assert.DoesNotContain(Observe(source.Replace("Console.Read()", "7")),
            finding => finding.Contains("Implicit", StringComparison.Ordinal));
    }

    [Fact]
    public void InstanceMethodForceIsResolved() => AssertTwins("""
        using System;
        class Probe {
         int Force(Lazy<int> lazy) {
          return lazy.Value;
         }
         static int Run() {
          return new Probe().Force(new Lazy<int>(() => Console.Read()));
         }
        }
        """, ["Force UnknownEffect 4", "<lazy> ImplicitInput 7", "Run ImplicitInput 7"], ["Force UnknownEffect 4"]);

    [Fact]
    public void ExtensionMethodForceIsResolved() => AssertTwins("""
        using System;
        static class Ext {
         public static int Force(this Lazy<int> lazy) {
          return lazy.Value;
         }
        }
        class Probe {
         static int Run() {
          Lazy<int> lazy = new(() => Console.Read());
          return lazy.Force();
         }
        }
        """, ["Force UnknownEffect 4", "<lazy> ImplicitInput 9", "Run ImplicitInput 10"], ["Force UnknownEffect 4"]);

    [Fact]
    public void LinqOverAnExplicitListParameterDoesNotAddDeferredUncertainty() => AssertTwins("""
        using System;
        using System.Collections.Generic;
        using System.Linq;
        class Probe {
         static IEnumerable<int> Build(List<int> values) {
          return values.Select(item => item + Console.Read());
         }
         static int Total(List<int> values) {
          var total = 0;
          var query = Build(values);
          foreach (var value in query) {
           total += value;
          }
          return total;
         }
        }
        """, ["<lambda> ImplicitInput 6", "Total ImplicitInput 11"], []);

    [Fact]
    public void ValueOnAnUntypedReceiverFailsClosed()
    {
        var findings = Observe("""
            using System;
            class Probe {
             static int Run(Func<Lazy<int>> make) {
              var lazy = make();
              return lazy.Value;
             }
            }
            """);
        Assert.Contains(findings, finding => finding.StartsWith("Run UnknownEffect", StringComparison.Ordinal));
    }
}
