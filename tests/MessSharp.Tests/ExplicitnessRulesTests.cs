using MessSharp.Model;
using MessSharp.Report;
using MessSharp.Rule;
using MessSharp.RuleSet;
using System.Text.Json;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitnessRulesTests
{

    private static List<Violation> Analyze(string ruleset, string source) =>
        Engine.Analyze(ModelBuilder.Parse("test.cs", source), new Loader().Load(ruleset));

    private static List<string> Descriptions(IEnumerable<Violation> vs, string ruleName) =>
        vs.Where(v => v.Rule.Name == ruleName).Select(v => v.Description).ToList();

    private static void MustHave(IEnumerable<Violation> vs, string ruleName, string description) =>
        Assert.Contains(description, Descriptions(vs, ruleName));

    private static void MustNotHave(IEnumerable<Violation> vs, params string[] ruleNames)
    {
        foreach (var n in ruleNames)
            Assert.Empty(Descriptions(vs, n));
    }


    [Fact]
    public void Explicitness_LoadsOnlyFunctionLevelRules()
    {
        var names = new Loader().Load("explicitness").SelectMany(s => s.Rules).Select(r => r.Name);
        Assert.Equal(new[] { "ImplicitInput", "ImplicitOutput", "UnknownEffect" }, names.OrderBy(n => n));
    }

    [Fact]
    public void ExplicitnessStrict_AddsInstanceStateRules()
    {
        var names = new Loader().Load("explicitness-strict").SelectMany(s => s.Rules).Select(r => r.Name);
        Assert.Equal(
            new[] { "ImplicitInput", "ImplicitInstanceInput", "ImplicitInstanceOutput", "ImplicitOutput", "UnknownEffect" },
            names.OrderBy(n => n));
    }

    [Fact]
    public void CSharp_DoesNotIncludeExplicitnessRules()
    {
        var names = new Loader().Load("csharp").SelectMany(s => s.Rules).Select(r => r.Name).ToList();
        Assert.DoesNotContain("ImplicitInput", names);
        Assert.DoesNotContain("ImplicitOutput", names);
    }

    [Fact]
    public void BareRuleRef_ResolvesStrictRule()
    {
        var tmpFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(tmpFile, @"<ruleset name=""Custom""><rule ref=""ImplicitInstanceOutput""/></ruleset>");
        try
        {
            var rule = Assert.Single(new Loader().Load(tmpFile).SelectMany(s => s.Rules));
            Assert.Equal("ImplicitInstanceOutput", rule.Name);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }


    private const string AddToTotal = @"
using System;
public class Cart {
    private static decimal total = 0;
    public static decimal AddToTotal(decimal amount) {
        Console.WriteLine(""Old total: "" + total);
        total += amount;
        return total;
    }
}";

    [Fact]
    public void AddToTotal_ReportsGlobalReadAsInput()
    {
        var vs = Analyze("explicitness", AddToTotal);
        Assert.Equal(
            new[] { "The method AddToTotal() has an implicit input: reads static member 'total'." },
            Descriptions(vs, "ImplicitInput"));
    }

    [Fact]
    public void AddToTotal_ReportsPrintAndGlobalWriteAsOutputs()
    {
        var vs = Analyze("explicitness", AddToTotal);
        Assert.Equal(
            new[]
            {
                "The method AddToTotal() has an implicit output: uses Console.WriteLine.",
                "The method AddToTotal() has an implicit output: writes static member 'total'.",
            },
            Descriptions(vs, "ImplicitOutput"));
    }

    [Fact]
    public void Calculation_ReportsNothing()
    {
        var src = @"
public class Cart {
    public static decimal AddToTotal(decimal total, decimal amount) {
        var sum = total + amount;
        amount = 0;
        return sum;
    }
}";
        MustNotHave(Analyze("explicitness", src), "ImplicitInput", "ImplicitOutput");
    }


    [Fact]
    public void ImplicitInput_ReadOfConstOrReadonlyStatic_NotFlagged()
    {
        var src = @"
public class Tax {
    private const decimal Rate = 0.1m;
    private static readonly decimal Floor = 1m;
    public static decimal Calc(decimal amount) => amount * Rate + Floor;
}";
        MustNotHave(Analyze("explicitness", src), "ImplicitInput");
    }

    [Fact]
    public void ImplicitInput_ClassQualifiedStaticRead_Flagged()
    {
        var src = @"
public class Counter {
    public static int Count { get; set; }
    public int Next() { return Counter.Count + 1; }
}";
        MustHave(Analyze("explicitness", src), "ImplicitInput",
            "The method Next() has an implicit input: reads static member 'Count'.");
    }

    [Fact]
    public void GenericClassQualifiedStaticAccess_Flagged()
    {
        var src = @"
public class Cache<T> {
    private static T _value;
    public static T Get() => Cache<T>._value;
    public static void Set(T v) { Cache<T>._value = v; }
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitInput", "The method Get() has an implicit input: reads static member '_value'.");
        MustHave(vs, "ImplicitOutput", "The method Set() has an implicit output: writes static member '_value'.");
    }

    [Fact]
    public void ImplicitInput_LocalShadowingStatic_NotFlagged()
    {
        var src = @"
public class Counter {
    private static int total;
    public int Next(int total) { return total + 1; }
    public int Other() { var total = 3; return total; }
}";
        MustNotHave(Analyze("explicitness", src), "ImplicitInput");
    }

    [Fact]
    public void ImplicitInput_ObjectInitializerKeyAndNameof_NotFlagged()
    {
        var src = @"
public class Counter {
    private static int Total;
    public object Make(int n) => new Counter2 { Total = n, Name = nameof(Total) };
}";
        MustNotHave(Analyze("explicitness", src), "ImplicitInput", "ImplicitOutput");
    }

    [Fact]
    public void ImplicitOutput_NestedTupleDeconstruction_ReportsEveryWrite()
    {
        var src = @"
public class C {
    private static int A;
    private static int B;
    private static int D;
    public static void Nested() { (A, (B, _)) = (1, (2, 3)); }
    public static void Deep() { ((D, _), _) = ((1, 2), 3); }
}";
        var vs = Analyze("explicitness", src);
        Assert.Equal(
            new[]
            {
                "The method Nested() has an implicit output: writes static member 'A'.",
                "The method Nested() has an implicit output: writes static member 'B'.",
                "The method Deep() has an implicit output: writes static member 'D'.",
            },
            Descriptions(vs, "ImplicitOutput"));
        MustNotHave(vs, "ImplicitInput");
    }

    [Theory]
    [InlineData("DateTime.Now", "var t = DateTime.Now;")]
    [InlineData("DateTime.UtcNow", "var t = System.DateTime.UtcNow;")]
    [InlineData("Guid.NewGuid", "var g = Guid.NewGuid();")]
    [InlineData("Environment.GetEnvironmentVariable", "var v = Environment.GetEnvironmentVariable(\"HOME\");")]
    [InlineData("Console.ReadLine", "var s = Console.ReadLine();")]
    [InlineData("File.ReadAllText", "var s = File.ReadAllText(\"a\");")]
    [InlineData("new Random()", "var r = new Random();")]
    public void ImplicitInput_AmbientSource_Flagged(string api, string statement)
    {
        var src = "public class Foo { public void Bar() { " + statement + " } }";
        MustHave(Analyze("explicitness", src), "ImplicitInput",
            $"The method Bar() has an implicit input: uses {api}.");
    }

    [Fact]
    public void ImplicitInput_SeededRandom_NotFlagged()
    {
        var src = "public class Foo { public int Bar(int seed) { return new Random(seed).Next(); } }";
        MustNotHave(Analyze("explicitness", src), "ImplicitInput");
    }

    [Theory]
    [InlineData("Random r = new();")]
    [InlineData("System.Random r = new();")]
    [InlineData("Random? r = new();")]
    [InlineData("Random a = new(1), r = new();")]
    public void ImplicitInput_TargetTypedUnseededRandom_Flagged(string statement)
    {
        var src = "public class Foo { public void Bar() { " + statement + " } }";
        MustHave(Analyze("explicitness", src), "ImplicitInput",
            "The method Bar() has an implicit input: uses new Random().");
    }

    [Theory]
    [InlineData("Random r = new(1);")]
    [InlineData("object r = new();")]
    [InlineData("List<int> r = new();")]
    public void ImplicitInput_TargetTypedSeededOrOtherType_NotFlagged(string statement)
    {
        var src = "public class Foo { public void Bar() { " + statement + " } }";
        MustNotHave(Analyze("explicitness", src), "ImplicitInput");
    }

    [Fact]
    public void ImplicitInput_RepeatedRead_ReportedOncePerMethodAtFirstUse()
    {
        var src = @"
public class Counter {
    private static int total;
    public int Twice() {
        var a = total;
        return a + total;
    }
}";
        var v = Assert.Single(Analyze("explicitness", src), x => x.Rule.Name == "ImplicitInput");
        Assert.Equal(5, v.BeginLine);
    }

    [Fact]
    public void ImplicitInput_InstanceFieldRead_NotFlaggedByDefaultRuleset()
    {
        var src = "public class Foo { private int _n; public int Get() { return _n; } }";
        MustNotHave(Analyze("explicitness", src), "ImplicitInput", "ImplicitInstanceInput");
    }


    [Theory]
    [InlineData("items.Add(x);")]
    [InlineData("items?.Add(x);")]
    [InlineData("items[0] = x;")]
    [InlineData("items.Capacity = x;")]
    [InlineData("items.Capacity++;")]
    public void ImplicitOutput_ArgumentMutation_Flagged(string statement)
    {
        var src = "public class Foo { public void Bar(List<int> items, int x) { " + statement + " } }";
        MustHave(Analyze("explicitness", src), "ImplicitOutput",
            "The method Bar() has an implicit output: changes argument 'items'.");
    }

    [Theory]
    [InlineData("public void Bar(List<int> items) => items.Add(1);", "method Bar()")]
    [InlineData("public void Bar(List<int> items) => items?.Add(1);", "method Bar()")]
    [InlineData("public async Task Bar(Db items) => await items.SaveAsync();", "method Bar()")]
    [InlineData("public async System.Threading.Tasks.ValueTask Bar(Db items) => await items.SaveAsync();", "method Bar()")]
    [InlineData("public Foo(List<int> items) => items.Add(1);", "constructor Foo()")]
    public void ImplicitOutput_DiscardedCallAsExpressionBody_Flagged(string member, string kindAndName)
    {
        var src = "public class Foo { " + member + " }";
        var findings = Analyze("explicitness", src);
        if (member.Contains("SaveAsync", StringComparison.Ordinal))
        {
            MustHave(findings, "UnknownEffect", $"The {kindAndName} has an unknown effect: unresolved call 'items.SaveAsync'.");
            MustNotHave(findings, "ImplicitOutput");
        }
        else
            MustHave(findings, "ImplicitOutput", $"The {kindAndName} has an implicit output: changes argument 'items'.");
    }

    [Fact]
    public void ImplicitOutput_DiscardedCallAsAccessorOrLocalFunctionBody_Flagged()
    {
        var src = @"
public class Foo {
    private static List<int> Log = new();
    public int Size { get => 0; set => Log.Add(value); }
    public void Bar(List<int> sink) { void Put() => sink.Add(1); Put(); }
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitOutput", "The method set_Size() has an implicit output: changes the object in static member 'Log'.");
        MustHave(vs, "ImplicitOutput", "The method Bar() has an implicit output: changes argument 'sink'.");
    }

    [Fact]
    public void ImplicitOutput_ReturnedMutatorIsStillAnOutput()
    {
        var src = @"
public class Foo {
    public bool Bar(List<int> items) => items.Remove(1);
    public Task Save(Db db) => db.SaveAsync();
    public async Task<int> Count(Db db) => await db.CountAsync();
    public int Size(List<int> items) { int Local() => items.IndexOf(1); return Local(); }
}";
        MustHave(Analyze("explicitness", src), "ImplicitOutput", "The method Bar() has an implicit output: changes argument 'items'.");
    }

    [Fact]
    public void ImplicitOutput_ArgumentOwnershipFollowsAliasesAndRebinding()
    {
        var src = @"
public class Changes {
    public void Alias(List<int> values) { var alias = values; alias.Add(1); }
    public List<int> Copy(List<int> values) { values = new List<int>(values); values.Add(1); return values; }
    public List<int> AfterRebind(List<int> values) { values = new List<int>(); values.Add(1); return values; }
    public void Conditional(List<int> values, bool copy) { if (copy) values = new List<int>(values); values.Add(1); }
    public void Before(List<int> values) { values.Add(1); values = new List<int>(); }
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitOutput", "The method Alias() has an implicit output: changes argument 'values'.");
        MustHave(vs, "ImplicitOutput", "The method Conditional() has an implicit output: changes argument 'values'.");
        MustHave(vs, "ImplicitOutput", "The method Before() has an implicit output: changes argument 'values'.");
        Assert.DoesNotContain(vs, v => v.Description.Contains("Copy()"));
        Assert.DoesNotContain(vs, v => v.Description.Contains("AfterRebind()"));
    }

    [Fact]
    public void ImplicitOutput_ShallowCopyKeepsSharedReachableElements()
    {
        var src = @"
public class Changes {
    public void Shared(List<List<int>> values) { var copy = new List<List<int>>(values); copy[0].Add(1); }
    public List<int> Detached(List<int> values) { var copy = (int[]) values.ToArray().Clone(); copy[0] = 1; return copy.ToList(); }
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitOutput", "The method Shared() has an implicit output: changes argument 'values'.");
        Assert.DoesNotContain(vs, v => v.Description.Contains("Detached()"));
        MustNotHave(vs.Where(v => v.Description.Contains("Detached()")), "UnknownEffect", "ImplicitOutput");
    }

    [Fact]
    public void ImplicitOutput_ParameterEffectsAreSubstitutedAtCallSite()
    {
        var src = @"
public class Changes {
    static void Leaf(List<int> values) { values.Add(1); }
    static void WithAmbient(List<int> values) { Console.WriteLine(values.Count); values.Add(1); }
    static void CallerOwned(List<int> values) { Leaf(values); }
    static List<int> FreshPrivate() { var values = new List<int>(); Leaf(values); return values; }
    static List<int> FreshPrivateWithAmbient() { var values = new List<int>(); WithAmbient(values); return values; }
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitOutput", "The method Leaf() has an implicit output: changes argument 'values'.");
        MustHave(vs, "ImplicitOutput", "The method CallerOwned() has an implicit output: changes argument 'values'.");
        MustHave(vs, "ImplicitOutput", "The method FreshPrivateWithAmbient() has an implicit output: uses Console.WriteLine.");
        Assert.DoesNotContain(vs, v => v.Description.Contains("FreshPrivate()"));
        Assert.DoesNotContain(vs, v => v.Description.Contains("FreshPrivateWithAmbient()")
            && v.Description.Contains("changes argument"));
    }

    [Fact]
    public void ImplicitOutput_ReturnedAliasMutationChangesCallerStorage()
    {
        var src = @"
public class Changes {
    static List<int> Alias(List<int> values) => values;
    static void Change(List<int> values) { Alias(values).Add(1); }
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitOutput", "The method Change() has an implicit output: changes argument 'values'.");
        Assert.DoesNotContain(vs, v => v.Description.Contains("Alias()"));
        MustNotHave(vs.Where(v => v.Description.Contains("Change()")), "UnknownEffect");
    }

    [Fact]
    public void Explicitness_UnknownAndKnownEffectsPropagateThroughCycles()
    {
        var src = @"
public class Probe {
    static int First(int value, Func<int, int> notify) => value <= 0 ? 0 : Second(value, notify);
    static int Second(int value, Func<int, int> notify) => notify(value) + First(value - 1, notify);
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "UnknownEffect", "The method First() has an unknown effect: unresolved call 'notify'.");
        MustHave(vs, "UnknownEffect", "The method Second() has an unknown effect: unresolved call 'notify'.");
        MustNotHave(vs, "ImplicitInput", "ImplicitOutput");
    }

    [Fact]
    public void Explicitness_ShadowedCallableParameterStaysUnknown()
    {
        var src = @"
public class Probe {
    static int Helper(int value) => value + 1;
    static int Send(int value, Func<int, int> Helper) => Helper(value);
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "UnknownEffect", "The method Send() has an unknown effect: unresolved call 'Helper'.");
        MustNotHave(vs, "ImplicitInput", "ImplicitOutput");
    }

    [Fact]
    public void Explicitness_FixedCaptureAndTopLevelPrivateMutationAreCalculations()
    {
        var src = @"
var values = new System.Collections.Generic.List<int>();
values.Add(1);
public class Probe {
    static int Run() { int offset = 1; Func<int, int> add = value => value + offset; return add(2); }
}";
        MustNotHave(Analyze("explicitness", src), "ImplicitInput", "ImplicitOutput", "UnknownEffect");
    }

    [Fact]
    public void Explicitness_CallbackCreationIsDeferredButInvocationTransfersEffects()
    {
        var src = @"
public class Probe {
    static Action Build() => () => Console.WriteLine(1);
    static void CreateOnly() { var action = Build(); }
    static void Invoke() { var action = Build(); action(); }
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitOutput", "The method <lambda>() has an implicit output: uses Console.WriteLine.");
        MustHave(vs, "ImplicitOutput", "The method Invoke() has an implicit output: uses Console.WriteLine.");
        Assert.DoesNotContain(vs, v => v.Description.Contains("Build()"));
        Assert.DoesNotContain(vs, v => v.Description.Contains("CreateOnly()"));
    }

    [Fact]
    public void Explicitness_CallbackArgumentUsesLexicalIdentity()
    {
        var src = @"
using System;
class Probe {
    static int Apply(int value, Func<int, int> f) => f(value);
    static int Pure(int value) => value + 1;
    static int Shadowed(int value, Func<int, int> Pure) => Apply(value, Pure);
    static int Known(int value) => Apply(value, Pure);
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "UnknownEffect", "The method Apply() has an unknown effect: unresolved call 'f'.");
        MustHave(vs, "UnknownEffect", "The method Shadowed() has an unknown effect: unresolved call 'f'.");
        Assert.DoesNotContain(vs, v => v.Description.Contains("Known()"));
        MustNotHave(vs, "ImplicitInput", "ImplicitOutput");
    }

    [Fact]
    public void Explicitness_CallbackParameterEffectsUseInvocationArguments()
    {
        var src = @"
using System; using System.Collections.Generic;
class Probe {
    static void Apply(List<int> value, Action<List<int>> f) { f(value); }
    static void Mutator(List<int> payload) { payload.Add(1); }
    static void PrivateWrapper() { var items = new List<int>(); Apply(items, Mutator); }
    static void CallerOwnedWrapper(List<int> items) { Apply(items, Mutator); }
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitOutput", "The method Mutator() has an implicit output: changes argument 'payload'.");
        MustHave(vs, "ImplicitOutput", "The method CallerOwnedWrapper() has an implicit output: changes argument 'items'.");
        Assert.DoesNotContain(vs, v => v.Description.Contains("PrivateWrapper()"));
        Assert.DoesNotContain(vs, v => v.Description.Contains("payload")
            && v.Description.Contains("CallerOwnedWrapper()"));
        MustHave(vs, "UnknownEffect", "The method Apply() has an unknown effect: unresolved call 'f'.");
    }

    [Fact]
    public void Explicitness_CallbackTransferJsonNamesActualCaller()
    {
        var vs = Analyze("explicitness", @"
using System; using System.Collections.Generic;
class Probe {
    static void Apply(List<int> value, Action<List<int>> f) { f(value); }
    static void Mutator(List<int> payload) { payload.Add(1); }
    static void Wrapper(List<int> items) { Apply(items, Mutator); }
}");
        using var output = new StringWriter();
        new JsonRenderer().Render(output, new Report.Report { Violations = vs });
        using var json = JsonDocument.Parse(output.ToString());
        var wrapper = json.RootElement.GetProperty("files")[0].GetProperty("violations").EnumerateArray()
            .Single(v => v.GetProperty("method").GetString() == "Wrapper");
        Assert.Equal("ImplicitOutput", wrapper.GetProperty("rule").GetString());
        Assert.Contains("changes argument 'items'", wrapper.GetProperty("description").GetString());
    }

    [Fact]
    public void Explicitness_LiteralFalseBranchDoesNotExecuteEffects()
    {
        var src = @"
using System;
public class Probe {
    static int Dead(int value) { if (false) { Console.WriteLine(value); ThirdParty.Process(value); } return value; }
    static int LiveThen(int value) { if (true) { Console.WriteLine(value); } else { ThirdParty.Process(value); } return value; }
    static int LiveElse(int value) { if (false) { ThirdParty.Process(value); } else { Console.WriteLine(value); } return value; }
}";
        var vs = Analyze("explicitness", src);
        Assert.DoesNotContain(vs, v => v.Description.Contains("Dead()"));
        MustHave(vs, "ImplicitOutput", "The method LiveThen() has an implicit output: uses Console.WriteLine.");
        MustHave(vs, "ImplicitOutput", "The method LiveElse() has an implicit output: uses Console.WriteLine.");
        MustNotHave(vs.Where(v => v.Description.Contains("LiveThen()") || v.Description.Contains("LiveElse()")),
            "UnknownEffect");
    }

    [Fact]
    public void Explicitness_JsonMetadataNamesCallbackOwner()
    {
        var vs = Analyze("explicitness", @"
public class Probe {
    static Action Build() => () => Console.WriteLine(1);
}");
        using var output = new StringWriter();
        new JsonRenderer().Render(output, new Report.Report { Violations = vs });
        using var json = JsonDocument.Parse(output.ToString());
        var violation = json.RootElement.GetProperty("files")[0].GetProperty("violations").EnumerateArray()
            .Single(v => v.GetProperty("rule").GetString() == "ImplicitOutput");
        Assert.Equal("Probe", violation.GetProperty("class").GetString());
        Assert.Equal("<lambda>", violation.GetProperty("method").GetString());
        Assert.Contains("<lambda>()", violation.GetProperty("description").GetString());
    }

    [Fact]
    public void ImplicitOutput_ArgumentQueryWithUsedResult_NotFlagged()
    {
        var src = @"
public class Foo {
    public int Bar(List<int> items) {
        var n = items.Count();
        return n + items.Where(i => i > 0).Sum();
    }
}";
        MustNotHave(Analyze("explicitness", src), "ImplicitOutput");
    }

    [Fact]
    public void ImplicitOutput_OutAndRefParameterWrites_Flagged()
    {
        var src = @"
public class Foo {
    public bool TryBar(ref int count, out int result) {
        count++;
        result = count;
        return true;
    }
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitOutput", "The method TryBar() has an implicit output: writes ref parameter 'count'.");
        MustHave(vs, "ImplicitOutput", "The method TryBar() has an implicit output: writes out parameter 'result'.");
    }

    [Fact]
    public void ImplicitOutput_MutationOfReadonlyStaticCollection_Flagged()
    {
        var src = @"
public class Cache {
    private static readonly Dictionary<string, int> Map = new();
    public void Put(string k, int v) { Map[k] = v; }
    public int Get(string k) => Map[k];
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitOutput", "The method Put() has an implicit output: changes the object in static member 'Map'.");
        MustHave(vs, "ImplicitInput", "The method Get() has an implicit input: reads static member 'Map'.");
        MustNotHave(Analyze("explicitness", @"class Lookup {
            private static readonly List<int> Values = new() { 1, 2 };
            public int Get(int index) => Values[index];
        }"), "ImplicitInput");
    }

    [Fact]
    public void ImplicitOutput_StaticConstructorInitialisingStatics_NotFlagged()
    {
        var src = @"
public class Registry {
    private static int count;
    static Registry() { count = 1; }
}";
        MustNotHave(Analyze("explicitness", src), "ImplicitInput", "ImplicitOutput");
    }

    [Fact]
    public void ImplicitOutput_InstanceConstructorBumpingStaticCounter_Flagged()
    {
        var src = @"
public class Widget {
    private static int created;
    private readonly int _id;
    public Widget() { _id = created++; }
}";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitOutput", "The constructor Widget() has an implicit output: writes static member 'created'.");
        MustHave(vs, "ImplicitInput", "The constructor Widget() has an implicit input: reads static member 'created'.");
    }

    [Theory]
    [InlineData("Console.Error", "Console.Error.WriteLine(\"x\");")]
    [InlineData("File.WriteAllText", "File.WriteAllText(\"a\", \"b\");")]
    [InlineData("Environment.Exit", "Environment.Exit(1);")]
    [InlineData("Debug.WriteLine", "System.Diagnostics.Debug.WriteLine(\"x\");")]
    public void ImplicitOutput_AmbientSink_Flagged(string api, string statement)
    {
        var src = "public class Foo { public void Bar() { " + statement + " } }";
        MustHave(Analyze("explicitness", src), "ImplicitOutput",
            $"The method Bar() has an implicit output: uses {api}.");
    }

    [Fact]
    public void ImplicitOutput_InstanceFieldWrite_ReportsCallerVisibleWrite()
    {
        var src = "public class Foo { private int _n; public void Set(int n) { _n = n; } public int Get() => _n; }";
        var vs = Analyze("explicitness", src);
        MustHave(vs, "ImplicitOutput", "The method Set() has an implicit output: writes member '_n'.");
        MustNotHave(vs, "ImplicitInput", "ImplicitInstanceInput", "ImplicitInstanceOutput", "UnknownEffect");
    }

    [Fact]
    public void ImplicitOutput_SuppressedOnMethod_NotReported()
    {
        var src = @"
public class Foo {
    [System.Diagnostics.CodeAnalysis.SuppressMessage(""MessSharp"", ""ImplicitOutput"")]
    public void Bar() { Console.WriteLine(""x""); }
}";
        MustNotHave(Analyze("explicitness", src), "ImplicitOutput");
    }


    private const string Account = @"
public class Account {
    private readonly List<string> _log = new();
    private decimal _balance;
    public string Owner { get; }
    public Account(string owner) { Owner = owner; _balance = 0; }
    public void Deposit(decimal amount) {
        this._balance += amount;
        _log.Add(Owner);
    }
    public static decimal Fee(decimal amount) => amount * 0.01m;
}";

    [Fact]
    public void Strict_MethodReadingMembers_ReportsInstanceInputs()
    {
        var vs = Analyze("explicitness-strict", Account);
        Assert.Equal(
            new[]
            {
                "The method Deposit() has an implicit input: reads member '_balance'.",
                "The method Deposit() has an implicit input: reads member '_log'.",
                "The method Deposit() has an implicit input: reads member 'Owner'.",
            },
            Descriptions(vs, "ImplicitInstanceInput"));
    }

    [Fact]
    public void Strict_MethodChangingMembers_ReportsInstanceOutputs()
    {
        var vs = Analyze("explicitness-strict", Account);
        Assert.Equal(
            new[]
            {
                "The method Deposit() has an implicit output: writes member '_balance'.",
                "The method Deposit() has an implicit output: changes the object in member '_log'.",
            },
            Descriptions(vs, "ImplicitInstanceOutput"));
    }

    [Fact]
    public void Strict_PrimaryConstructorParameter_IsInstanceState()
    {
        var src = "public class Greeter(string name) { public string Greet() => \"Hi \" + name; }";
        MustHave(Analyze("explicitness-strict", src), "ImplicitInstanceInput",
            "The method Greet() has an implicit input: reads member 'name'.");
    }


    private const string Ledger = @"
public class Ledger {
    private static int _count;
    private static readonly List<string> Names = new();
    private int _total;
    public void Post(int amount, ref int balance, out int seen) {
        _count++;
        Names.Add(""post"");
        _total = amount;
        balance += _total;
        seen = _count;
        Console.WriteLine(amount);
    }
}";

    private static List<string> RuleAndDescription(IEnumerable<Violation> vs) =>
        vs.Select(v => v.Rule.Name + ": " + v.Description).ToList();

    [Fact]
    public void Strict_AllFourRulesOnOneMethod_KeepTheirDistinctionsAndReportOrder()
    {
        Assert.Equal(
            new[]
            {
                "ImplicitInput: The method Post() has an implicit input: reads static member '_count'.",
                "ImplicitInput: The method Post() has an implicit input: reads static member 'Names'.",
                "ImplicitOutput: The method Post() has an implicit output: writes static member '_count'.",
                "ImplicitOutput: The method Post() has an implicit output: changes the object in static member 'Names'.",
                "ImplicitOutput: The method Post() has an implicit output: writes ref parameter 'balance'.",
                "ImplicitOutput: The method Post() has an implicit output: writes out parameter 'seen'.",
                "ImplicitOutput: The method Post() has an implicit output: uses Console.WriteLine.",
                "ImplicitInstanceInput: The method Post() has an implicit input: reads member '_total'.",
                "ImplicitInstanceOutput: The method Post() has an implicit output: writes member '_total'.",
            },
            RuleAndDescription(Analyze("explicitness-strict", Ledger)));
    }

    [Theory]
    [InlineData("ImplicitInput")]
    [InlineData("ImplicitOutput")]
    [InlineData("ImplicitInstanceInput")]
    [InlineData("ImplicitInstanceOutput")]
    public void Strict_OneRuleAlone_ReportsOnlyItsOwnFindings(string ruleName)
    {
        var strict = Analyze("explicitness-strict", Ledger);
        var rule = new Loader().Load("explicitness-strict").SelectMany(s => s.Rules).Single(r => r.Name == ruleName);
        var alone = Engine.Analyze(ModelBuilder.Parse("test.cs", Ledger), new[] { rule });
        Assert.NotEmpty(alone);
        Assert.Equal(Descriptions(strict, ruleName), Descriptions(alone, ruleName));
        Assert.All(alone, v => Assert.Equal(ruleName, v.Rule.Name));
    }

    [Fact]
    public void SuccessiveAnalyses_DoNotShareResults()
    {
        var first = Analyze("explicitness", "public class Foo { private static int x; public int Get() => x; }");
        var second = Analyze("explicitness", "public class Foo { private const int x = 1; public int Get() => x; }");
        MustHave(first, "ImplicitInput", "The method Get() has an implicit input: reads static member 'x'.");
        MustNotHave(second, "ImplicitInput");
    }

    [Fact]
    public void PartialType_StateFromOtherFile_SeenByAllRules()
    {
        var files = new[]
        {
            ModelBuilder.Parse("a.cs", "public partial class Foo { private static int _n; private int _m; }"),
            ModelBuilder.Parse("b.cs", "public partial class Foo { public void Bump() { _n++; _m++; } }"),
        };
        var vs = Engine.AnalyzeAll(files, new Loader().Load("explicitness-strict"));
        MustHave(vs, "ImplicitInput", "The method Bump() has an implicit input: reads static member '_n'.");
        MustHave(vs, "ImplicitOutput", "The method Bump() has an implicit output: writes static member '_n'.");
        MustHave(vs, "ImplicitInstanceInput", "The method Bump() has an implicit input: reads member '_m'.");
        MustHave(vs, "ImplicitInstanceOutput", "The method Bump() has an implicit output: writes member '_m'.");
    }
}
