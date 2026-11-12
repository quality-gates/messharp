using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessExecutionTests
{
    private static List<Violation> Analyze(string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load("explicitness"));

    private static IEnumerable<Violation> Owner(IEnumerable<Violation> findings, string name) =>
        findings.Where(finding => finding.Method == name);

    [Fact]
    public void ForwardedCallbacksBindRenamedArgumentsAndKeepFreshStoragePrivate()
    {
        var findings = Analyze("""
            class Probe {
                static void Apply(List<int> value, Action<List<int>> f) { f(value); }
                static void Forward(List<int> data, Action<List<int>> h) { Apply(data, h); }
                static void Fresh(List<int> data, Action<List<int>> h) { Apply(new List<int>(), h); }
                static void Mutator(List<int> payload) { payload.Add(1); }
                static void Shared(List<int> items) { Forward(items, Mutator); }
                static void Detached(List<int> items) { Fresh(items, Mutator); }
            }
            """);
        Assert.Equal(new[] { "ImplicitOutput" }, Owner(findings, "Shared").Select(f => f.Rule.Name));
        Assert.Equal("The method Shared() has an implicit output: changes argument 'items'.",
            Owner(findings, "Shared").Single().Description);
        Assert.Empty(Owner(findings, "Detached"));
    }

    [Theory]
    [InlineData("items")]
    [InlineData("value")]
    public void CallbackCapturesRetainTheCallersBindingWhenHelperNamesCollide(string helperParameter)
    {
        var findings = Analyze("class Probe { static void Apply(List<int> " + helperParameter
            + ", Action<List<int>> f) { f(" + helperParameter + "); } " + """
                static void Shared(List<int> items) { Apply(new List<int>(), ignored => items.Add(1)); }
                static void Detached() { var items = new List<int>(); Apply(items, ignored => items.Add(1)); }
            }
            """);
        Assert.Equal(new[] { "ImplicitOutput" }, Owner(findings, "Shared").Select(f => f.Rule.Name));
        Assert.Empty(Owner(findings, "Detached"));
    }

    [Fact]
    public void ReturnedClosuresInstantiateFactoryArgumentsAtInvocation()
    {
        var findings = Analyze("""
            class Probe {
                static Action Build(List<int> values) => () => values.Add(1);
                static void Shared(List<int> items) { Build(items)(); }
                static void Detached() { Build(new List<int>())(); }
            }
            """);
        Assert.Equal(new[] { "ImplicitOutput" }, Owner(findings, "Shared").Select(f => f.Rule.Name));
        Assert.Empty(Owner(findings, "Build"));
        Assert.Empty(Owner(findings, "Detached"));
    }

    [Theory]
    [InlineData("var alias = new List<int>(); while (again) { alias.Add(1); alias = values; }")]
    [InlineData("var alias = new List<int>(); for (var i = 0; i < 2; i++) { alias.Add(1); alias = values; }")]
    [InlineData("var alias = values ?? new List<int>(); alias.Add(1);")]
    [InlineData("var alias = values as List<int>; alias.Add(1);")]
    [InlineData("var alias = values!; alias.Add(1);")]
    [InlineData("var alias = again switch { true => values, false => new List<int>() }; alias.Add(1);")]
    [InlineData("List<int> alias = null; alias ??= values; alias.Add(1);")]
    public void StorageChoicesAndLoopCarriedAliasesKeepSharedOrigins(string body)
    {
        var findings = Analyze("class Probe { static void Shared(List<int> values, bool again) { " + body
            + " } static void Detached(List<int> values) { var copy = new List<int>(values); copy.Add(1); } }");
        Assert.Contains(Owner(findings, "Shared"), finding => finding.Rule.Name == "ImplicitOutput"
            && finding.Description == "The method Shared() has an implicit output: changes argument 'values'.");
        Assert.Empty(Owner(findings, "Detached"));
    }

    [Fact]
    public void ShallowCopiesKeepElementOriginsWithoutSharingTheirOuterContainer()
    {
        var findings = Analyze("""
            class Probe {
                static void Shared(List<List<int>> values) { List<List<int>> copy = [.. values]; copy[0].Add(1); }
                static void Detached(List<List<int>> values) { List<List<int>> copy = [.. values]; copy[0] = new List<int>(); }
            }
            """);
        Assert.Equal(new[] { "ImplicitOutput" }, Owner(findings, "Shared").Select(f => f.Rule.Name));
        Assert.Empty(Owner(findings, "Detached"));
    }

    [Fact]
    public void IteratorCreationIsQuietAndEnumerationReportsTheExecutionSite()
    {
        var findings = Analyze("""
            class Probe {
                static IEnumerable<int> Generate() { Console.WriteLine(1); yield return 1; }
                static IEnumerable<int> Start() => Generate();
                static void Force() {
                    var values = Generate();
                    foreach (var value in values) { }
                }
            }
            """);
        Assert.Empty(Owner(findings, "Start"));
        var forced = Owner(findings, "Force").Single();
        Assert.Equal("ImplicitOutput", forced.Rule.Name);
        Assert.Equal(6, forced.BeginLine);
        Assert.Equal("The method Force() has an implicit output: uses Console.WriteLine.", forced.Description);
    }

    [Fact]
    public void SequenceConsumersKeepMaterializedValuesQuietAndUnknownSequencesConservative()
    {
        var findings = Analyze("""
            using System.Collections.Generic;
            using System.Linq;
            class Probe {
                static List<int> Capacity() => new List<int>(1 + 2);
                static List<int> ComputedCapacity(int n) => new List<int>(System.Math.Abs(n));
                static List<int> FromArray() => new List<int>(new[] { 1 });
                static void AddArray() { var list = new List<int>(); list.AddRange(new[] { 1 }); }
                static bool Opaque(IEnumerable<int> values) => values.Contains(1);
            }
            """);
        Assert.Empty(Owner(findings, "Capacity"));
        Assert.Empty(Owner(findings, "ComputedCapacity"));
        Assert.Empty(Owner(findings, "FromArray"));
        Assert.Empty(Owner(findings, "AddArray"));
        var unknown = Assert.Single(Owner(findings, "Opaque"));
        Assert.Equal("UnknownEffect", unknown.Rule.Name);
        Assert.Equal(8, unknown.BeginLine);
    }

    [Fact]
    public void NonEnumeratingCustomExtensionsDoNotForceSequences()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            static class NonEnumeratingExtensions {
                public static bool Contains(this IEnumerable<int> source, string value) => false;
                public static void AddRange(this HashSet<int> target, IEnumerable<int> source) { }
            }
            class Probe {
                static IEnumerable<int> Gen() { yield return Console.Read(); }
                static bool CustomContains() { var sequence = Gen(); return sequence.Contains("not an integer"); }
                static void CustomAddRange() { var sequence = Gen(); var target = new HashSet<int>(); target.AddRange(sequence); }
            }
            """);
        Assert.Empty(Owner(findings, "CustomContains"));
        Assert.Empty(Owner(findings, "CustomAddRange"));
    }

    [Fact]
    public void AmbiguousCustomContainsIsNotAssumedToEnumerate()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            static class Extensions {
                public static bool Contains(this IEnumerable<int> source, string value) => false;
                public static bool Contains(this IEnumerable<int> source, object value) => false;
            }
            class Probe {
                static IEnumerable<int> Gen() { yield return Console.Read(); }
                static bool Run(string key) { var q = Gen(); return q.Contains(key); }
            }
            """);
        var unknown = Assert.Single(Owner(findings, "Run"));
        Assert.Equal(("UnknownEffect", 10, "The method Run() has an unknown effect: unresolved sequence consumer."),
            (unknown.Rule.Name, unknown.BeginLine, unknown.Description));
    }

    [Fact]
    public void UnrelatedStringExtensionDoesNotMakeAPureIteratorUnknown()
    {
        var findings = Analyze("""
            using System.Collections.Generic;
            using System.Linq;
            static class Extensions {
                public static bool Contains(this string source, int value) => false;
            }
            class Probe {
                static IEnumerable<int> Pure() { yield return 7; }
                static bool Run() { var q = Pure(); return q.Contains(1); }
            }
            """);
        Assert.Empty(Owner(findings, "Run"));
    }

    [Fact]
    public void StandardContainsUsesVariableArgumentTypeToRejectAnIncompatibleExtension()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            static class Extensions {
                public static bool Contains(this IEnumerable<int> source, string value) => false;
            }
            class Probe {
                static IEnumerable<int> Gen() { yield return Console.Read(); }
                static IEnumerable<int> Pure() { yield return 7; }
                static bool Action(int key) { var q = Gen(); return q.Contains(key); }
                static bool PureAction(int key) { var q = Pure(); return q.Contains(key); }
            }
            """);
        var action = Assert.Single(Owner(findings, "Action"));
        Assert.Equal(("ImplicitInput", 10), (action.Rule.Name, action.BeginLine));
        Assert.Empty(Owner(findings, "PureAction"));
    }

    [Fact]
    public void NarrowingNumericExtensionsDoNotHideStandardContains()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            static class Extensions {
                public static bool Contains(this IEnumerable<int> source, short value) => false;
                public static bool Contains(this IEnumerable<int> source, ushort value) => false;
            }
            class Probe {
                static IEnumerable<int> Gen() { yield return Console.Read(); }
                static IEnumerable<int> Pure() { yield return 7; }
                static bool Action(int key) { var q = Gen(); return q.Contains(key); }
                static bool PureAction(int key) { var q = Pure(); return q.Contains(key); }
            }
            """);
        var action = Assert.Single(Owner(findings, "Action"));
        Assert.Equal(("ImplicitInput", 11), (action.Rule.Name, action.BeginLine));
        Assert.Empty(Owner(findings, "PureAction"));
    }

    [Fact]
    public void IncompatibleCustomExtensionEffectsDoNotReachStandardContains()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            static class Extensions {
                public static bool Contains(this IEnumerable<int> source, string value) { Console.WriteLine("custom"); return false; }
            }
            class Probe {
                static IEnumerable<int> Pure() { yield return 7; }
                static bool Run(int key) { var q = Pure(); return q.Contains(key); }
            }
            """);
        Assert.Empty(Owner(findings, "Run"));
    }

    [Fact]
    public void CompatibleCustomExtensionTransfersOnlyItsOwnEffects()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            static class Extensions {
                public static bool Contains(this IEnumerable<int> source, string value) { Console.WriteLine(value); return false; }
            }
            class Probe {
                static IEnumerable<int> Gen() { yield return Console.Read(); }
                static bool Run(string key) { var q = Gen(); return q.Contains(key); }
            }
            """);
        var result = Assert.Single(Owner(findings, "Run"));
        Assert.Equal(("ImplicitOutput", 9, "The method Run() has an implicit output: uses Console.WriteLine."),
            (result.Rule.Name, result.BeginLine, result.Description));
    }

    [Fact]
    public void UnrelatedNumericExtensionDoesNotMakeAPureIteratorUnknown()
    {
        var findings = Analyze("""
            using System.Collections.Generic;
            using System.Linq;
            static class Extensions {
                public static bool Contains(this int source, int value) => false;
            }
            class Probe {
                static IEnumerable<int> Pure() { yield return 7; }
                static bool Run() { var q = Pure(); return q.Contains(1); }
            }
            """);
        Assert.Empty(Owner(findings, "Run"));
    }

    [Fact]
    public void AliasedCustomEnumerableDoesNotAcquireIteratorEffects()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            namespace Sample {
                using Enumerable = NonConsumer;
                static class NonConsumer {
                    public static bool Contains(IEnumerable<int> source, int value) => false;
                }
                class Probe {
                    static IEnumerable<int> Gen() { yield return Console.Read(); }
                    static bool Run() => Enumerable.Contains(Gen(), 1);
                }
            }
            """);
        Assert.DoesNotContain(Owner(findings, "Run"), finding => finding.Rule.Name == "ImplicitInput");
    }

    [Fact]
    public void StandardListContainsWinsOverAnExtensionWithTheSameArguments()
    {
        var findings = Analyze("""
            using System.Collections.Generic;
            using System.Linq;
            static class Extensions {
                public static bool Contains(this IEnumerable<int> source, int value) => false;
            }
            class Probe {
                static bool Run(List<int> values) => values.Contains(1);
                static bool HashSetRun(HashSet<int> values) => values.Contains(1);
            }
            """);
        Assert.Empty(Owner(findings, "Run"));
        Assert.Empty(Owner(findings, "HashSetRun"));
    }

    [Fact]
    public void StandardListAddRangeWinsOverAnUnrelatedExtension()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            static class Extensions {
                public static void AddRange(this List<int> target, string source) { }
            }
            class Probe {
                static IEnumerable<int> Gen() { yield return Console.Read(); }
                static void Run() { var q = Gen(); var xs = new List<int>(); xs.AddRange(q); }
            }
            """);
        var result = Assert.Single(Owner(findings, "Run"));
        Assert.Equal(("ImplicitInput", 8), (result.Rule.Name, result.BeginLine));
    }

    [Fact]
    public void NonListAddRangeExtensionsDoNotForceTheirArguments()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            static class Extensions {
                public static void AddRange(this ICollection<int> target, IEnumerable<int> source) { }
            }
            class Probe {
                static IEnumerable<int> Gen() { yield return Console.Read(); }
                static void Run() { var q = Gen(); var xs = new HashSet<int>(); xs.AddRange(q); }
            }
            """);
        Assert.Empty(Owner(findings, "Run"));
    }

    [Fact]
    public void ConvertedReceiverWithUnresolvedCustomContainsIsConservative()
    {
        var findings = Analyze("""
            using System.Collections.Generic;
            using System.Linq;
            static class Extensions {
                public static bool Contains(this IEnumerable<int> source, string value) => false;
            }
            class Probe {
                static bool Run() { var values = new List<int>(); return values.Contains("x"); }
            }
            """);
        var unknown = Assert.Single(Owner(findings, "Run"));
        Assert.Equal(("UnknownEffect", 7), (unknown.Rule.Name, unknown.BeginLine));
    }

    [Fact]
    public void ListConstructionForcesAnAliasedSequence()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            using Seq = System.Collections.Generic.IEnumerable<int>;
            using Buffer = System.Collections.Generic.List<int>;
            class Probe {
                static IEnumerable<int> Gen() { yield return Console.Read(); }
                static IEnumerable<int> Pure() { yield return 7; }
                static List<int> Action() { Seq sequence = Gen(); return new List<int>(sequence); }
                static List<int> PureAction() { Seq sequence = Pure(); return new List<int>(sequence); }
                static List<int> Opaque(Seq sequence) => new List<int>(sequence);
                static Buffer AliasTarget() => new Buffer(Gen());
                static bool Materialized(Buffer values) => values.Contains(1);
            }
            """);
        var action = Assert.Single(Owner(findings, "Action"));
        Assert.Equal(("ImplicitInput", 8, "The method Action() has an implicit input: uses Console.Read."),
            (action.Rule.Name, action.BeginLine, action.Description));
        Assert.Empty(Owner(findings, "PureAction"));
        var unknown = Assert.Single(Owner(findings, "Opaque"));
        Assert.Equal(("UnknownEffect", 10), (unknown.Rule.Name, unknown.BeginLine));
        var aliasTarget = Assert.Single(Owner(findings, "AliasTarget"));
        Assert.Equal(("ImplicitInput", 11), (aliasTarget.Rule.Name, aliasTarget.BeginLine));
        Assert.Empty(Owner(findings, "Materialized"));
    }

    [Fact]
    public void QualifiedStandardListIsNotHiddenByAnUnrelatedCustomList()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            namespace LocalTypes { class List<T> { public List(IEnumerable<T> q) { } } }
            namespace ProbeSpace {
                class Probe {
                    static IEnumerable<int> Gen() { yield return Console.Read(); }
                    static System.Collections.Generic.List<int> Action() => new System.Collections.Generic.List<int>(Gen());
                    static LocalTypes.List<int> Custom() => new LocalTypes.List<int>(Gen());
                }
            }
            """);
        var action = Assert.Single(Owner(findings, "Action"));
        Assert.Equal(("ImplicitInput", 7), (action.Rule.Name, action.BeginLine));
        Assert.Empty(Owner(findings, "Custom"));
    }

    [Fact]
    public void StaticEnumerableContainsForcesTheSequenceWithoutInventingEffects()
    {
        var findings = Analyze("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            class Probe {
                static IEnumerable<int> Gen() { yield return Console.Read(); }
                static IEnumerable<int> Pure() { yield return 7; }
                static bool Action() => Enumerable.Contains(Gen(), 1);
                static bool PureAction() => Enumerable.Contains(Pure(), 1);
            }
            """);
        var action = Assert.Single(Owner(findings, "Action"));
        Assert.Equal(("ImplicitInput", 7, "The method Action() has an implicit input: uses Console.Read."),
            (action.Rule.Name, action.BeginLine, action.Description));
        Assert.Empty(Owner(findings, "PureAction"));
    }

    [Fact]
    public void LazyCreationIsQuietAndValueAccessForcesTheLazyOwner()
    {
        var findings = Analyze("""
            class Probe {
                static Lazy<long> Make() => new Lazy<long>(() => DateTime.Now.Ticks);
                static long Force() => Make().Value;
            }
            """);
        Assert.Empty(Owner(findings, "Make"));
        Assert.Equal(new[] { "ImplicitInput" }, Owner(findings, "<lazy>").Select(f => f.Rule.Name));
        Assert.Equal(new[] { "ImplicitInput" }, Owner(findings, "Force").Select(f => f.Rule.Name));
        Assert.Equal(3, Owner(findings, "Force").Single().BeginLine);
    }

    [Theory]
    [InlineData("values.Any(value => { Console.WriteLine(value); return true; });")]
    [InlineData("values.Count(value => { Console.WriteLine(value); return true; });")]
    [InlineData("values.Sort((left, right) => { Console.WriteLine(left); return left - right; });")]
    public void EagerLibraryOperationsExecuteTheirCallbacks(string statement)
    {
        var findings = Analyze("class Probe { static void Force(List<int> values) { " + statement + " } }");
        Assert.Contains(Owner(findings, "Force"), finding => finding.Rule.Name == "ImplicitOutput"
            && finding.Description == "The method Force() has an implicit output: uses Console.WriteLine.");
        Assert.DoesNotContain(Owner(findings, "Force"), finding => finding.Rule.Name == "UnknownEffect");
    }

    [Fact]
    public void EscapedMutableCaptureReportsStateAtItsCallerButCreationIsQuiet()
    {
        var findings = Analyze("""
            class Probe {
                static Func<int> Counter() { var count = 0; return () => ++count; }
                static int Use() { var next = Counter(); return next() + next(); }
                static Func<int> Fixed(int value) => () => value + 1;
            }
            """);
        Assert.Empty(Owner(findings, "Counter"));
        Assert.Equal(new[]
        {
            ("ImplicitInput", 3, "The method Use() has an implicit input: reads captured value 'count'."),
            ("ImplicitOutput", 3, "The method Use() has an implicit output: writes captured value 'count'."),
        }, Owner(findings, "Use").Select(f => (f.Rule.Name, f.BeginLine, f.Description)).OrderBy(f => f.Name, StringComparer.Ordinal));
        Assert.Empty(Owner(findings, "Fixed"));
    }

    [Theory]
    [InlineData("value += 1;")]
    [InlineData("value = value + 1;")]
    [InlineData("value++;")]
    public void CapturedScalarUpdatesKeepTheirKnownEffects(string statement)
    {
        var findings = Analyze("class Probe { static Func<int> Make(int value) { return () => {\n"
            + statement + "\nreturn value; }; } }");
        Assert.Empty(Owner(findings, "Make"));
        var closure = Owner(findings, "<lambda>").ToList();
        Assert.Equal(new[] { "ImplicitInput", "ImplicitOutput" }, closure.Select(finding => finding.Rule.Name).OrderBy(name => name));
        Assert.All(closure, finding => Assert.Equal(2, finding.BeginLine));
    }

    [Fact]
    public void NestedCaptureRebindingCannotMakeALaterMutationLookPrivate()
    {
        var findings = Analyze("""
            class Probe {
                static void Shared(List<int> values) {
                    var alias = new List<int>();
                    Action rebind = () => alias = values;
                    rebind();
                    alias.Add(1);
                }
            }
            """);
        Assert.Contains(Owner(findings, "Shared"), finding => finding.Rule.Name == "UnknownEffect");
    }

    [Theory]
    [InlineData("static int Value => DateTime.Now.Second; static int Read() => Value;")]
    [InlineData("public int this[int index] { get { Console.WriteLine(index); return index; } } static int Read(Probe value) => value[0];")]
    [InlineData("static int Value { set { Console.WriteLine(value); } } static void Read() { Value = 1; }")]
    [InlineData("static void Read() { var writer = new System.IO.StreamWriter(\"log.txt\"); }")]
    public void UnsupportedPropertyIndexerAndConstructorDispatchRemainUnknown(string members)
    {
        var findings = Analyze("class Probe { " + members + " static int Quiet(int value) => value + 1; }");
        Assert.Contains(Owner(findings, "Read"), finding => finding.Rule.Name == "UnknownEffect");
        Assert.Empty(Owner(findings, "Quiet"));
    }

    [Fact]
    public void DeclaredConstructorEffectsTransferToTheAllocationSite()
    {
        var findings = Analyze("""
            class Logger { public Logger() { Console.WriteLine(1); } }
            class Probe { static Logger Make() => new Logger(); }
            """);
        Assert.Equal(new[] { "ImplicitOutput" }, Owner(findings, "Make").Select(f => f.Rule.Name));
        Assert.Equal("The method Make() has an implicit output: uses Console.WriteLine.", Owner(findings, "Make").Single().Description);
    }

    [Fact]
    public void ExportedReadonlyCollectionCanBeMutatedOutsideTheDeclaringClass()
    {
        var findings = Analyze("""
            class Probe {
                public static readonly List<int> Values = new() { 1 };
                private static readonly List<int> Fixed = new() { 1 };
                static int Read() => Values[0];
                static int Quiet() => Fixed[0];
            }
            """);
        Assert.Equal(new[] { "ImplicitInput" }, Owner(findings, "Read").Select(f => f.Rule.Name));
        Assert.Empty(Owner(findings, "Quiet"));
    }

    [Fact]
    public void ReadonlyCollectionMutatedThroughAReturnedAliasRemainsMutableState()
    {
        var findings = Analyze("""
            class Probe {
                private static readonly List<int> Values = new() { 1 };
                private static readonly List<int> Fixed = new() { 1 };
                static List<int> Alias() => Values;
                static void Change() { Alias().Add(1); }
                static int Read() => Values.Count;
                static int Quiet() => Fixed.Count;
            }
            """);
        Assert.Equal(new[] { "ImplicitInput" }, Owner(findings, "Read").Select(f => f.Rule.Name));
        Assert.Equal("The method Read() has an implicit input: reads static member 'Values'.", Owner(findings, "Read").Single().Description);
        Assert.Contains(Owner(findings, "Change"), finding => finding.Rule.Name == "ImplicitOutput");
        Assert.Empty(Owner(findings, "Quiet"));
    }
}
