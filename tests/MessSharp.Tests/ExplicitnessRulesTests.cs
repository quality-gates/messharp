using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

/// <summary>
/// Behavioral tests for the explicitness and explicitness-strict rulesets.
/// Each test loads the bundled ruleset XML, so the rule wiring is exercised too.
/// </summary>
public class ExplicitnessRulesTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Ruleset wiring
    // -------------------------------------------------------------------------

    [Fact]
    public void Explicitness_LoadsOnlyFunctionLevelRules()
    {
        var names = new Loader().Load("explicitness").SelectMany(s => s.Rules).Select(r => r.Name);
        Assert.Equal(new[] { "ImplicitInput", "ImplicitOutput" }, names.OrderBy(n => n));
    }

    [Fact]
    public void ExplicitnessStrict_AddsInstanceStateRules()
    {
        var names = new Loader().Load("explicitness-strict").SelectMany(s => s.Rules).Select(r => r.Name);
        Assert.Equal(
            new[] { "ImplicitInput", "ImplicitInstanceInput", "ImplicitInstanceOutput", "ImplicitOutput" },
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

    // -------------------------------------------------------------------------
    // ImplicitInput / ImplicitOutput: the book's add_to_total example
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // ImplicitInput
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // ImplicitOutput
    // -------------------------------------------------------------------------

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
        MustHave(Analyze("explicitness", src), "ImplicitOutput",
            $"The {kindAndName} has an implicit output: changes argument 'items'.");
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
    public void ImplicitOutput_CallAsReturnedExpressionBody_NotFlagged()
    {
        var src = @"
public class Foo {
    public bool Bar(List<int> items) => items.Remove(1);
    public Task Save(Db db) => db.SaveAsync();
    public async Task<int> Count(Db db) => await db.CountAsync();
    public int Size(List<int> items) { int Local() => items.IndexOf(1); return Local(); }
}";
        MustNotHave(Analyze("explicitness", src), "ImplicitOutput");
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
        MustNotHave(vs, "ImplicitInput");
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
    public void ImplicitOutput_InstanceFieldWrite_NotFlaggedByDefaultRuleset()
    {
        var src = "public class Foo { private int _n; public void Set(int n) { _n = n; } }";
        MustNotHave(Analyze("explicitness", src), "ImplicitOutput", "ImplicitInstanceOutput");
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

    // -------------------------------------------------------------------------
    // ImplicitInstanceInput / ImplicitInstanceOutput (explicitness-strict)
    // -------------------------------------------------------------------------

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
}
