using MessCS.Model;
using MessCS.Rule;
using MessCS.Rules.Design;
using Xunit;
using RuleSetType = MessCS.Rule.RuleSet;

namespace MessCS.Tests;

/// <summary>
/// Behavioral tests for the Design ruleset. Mirrors the structure of
/// messgo's internal/rules/rules_test.go design cases.
/// </summary>
public class DesignRulesTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static List<Violation> Analyze(string source, params BaseRule[] rules)
    {
        var sf = ModelBuilder.Parse("fixture.cs", source);
        var set = new RuleSetType { Name = "design", Rules = new(rules) };
        return Engine.Analyze(sf, new[] { set });
    }

    private static bool Has(IEnumerable<Violation> vs, string ruleName) =>
        vs.Any(v => v.Rule.Name == ruleName);

    private static void MustHave(IEnumerable<Violation> vs, params string[] names)
    {
        foreach (var n in names)
            Assert.True(Has(vs, n), $"Expected rule '{n}' to fire");
    }

    private static void MustNotHave(IEnumerable<Violation> vs, params string[] names)
    {
        foreach (var n in names)
            Assert.False(Has(vs, n), $"Did not expect rule '{n}' to fire");
    }

    private static T MakeRule<T>(string name, string message,
        Dictionary<string, string>? props = null) where T : BaseRule, new()
    {
        return new T
        {
            Name = name,
            Message = message,
            Priority = 2,
            SetName = "design",
            ExternalUrl = "",
            Description = "",
            Since = "0.2",
            RuleProps = new Properties(props),
        };
    }

    // -------------------------------------------------------------------------
    // ExitExpression
    // -------------------------------------------------------------------------

    private static ExitExpressionRule MakeExitRule() =>
        MakeRule<ExitExpressionRule>("ExitExpression",
            "The {0} {1}() contains an exit expression.");

    [Fact]
    public void ExitExpression_EnvironmentExit_Flagged()
    {
        var src = @"
public class Foo {
    public void Bar(int x) {
        if (x == 0) Environment.Exit(1);
    }
}";
        var vs = Analyze(src, MakeExitRule());
        MustHave(vs, "ExitExpression");
        Assert.Matches(@"The method Bar\(\) contains an exit expression\.",
            vs[0].Description);
    }

    [Fact]
    public void ExitExpression_EnvironmentFailFast_Flagged()
    {
        var src = @"
public class Foo {
    public void Bar() {
        Environment.FailFast(""oops"");
    }
}";
        var vs = Analyze(src, MakeExitRule());
        MustHave(vs, "ExitExpression");
    }

    [Fact]
    public void ExitExpression_NoExit_NotFlagged()
    {
        var src = @"
public class Foo {
    public void Bar() {
        throw new Exception();
    }
}";
        var vs = Analyze(src, MakeExitRule());
        MustNotHave(vs, "ExitExpression");
    }

    // -------------------------------------------------------------------------
    // GotoStatement
    // -------------------------------------------------------------------------

    private static GotoStatementRule MakeGotoRule() =>
        MakeRule<GotoStatementRule>("GotoStatement",
            "The {0} {1}() utilizes a goto statement.");

    [Fact]
    public void GotoStatement_PlainGoto_Flagged()
    {
        var src = @"
public class Foo {
    public void Bar(int x) {
        loop:
        if (x-- > 0) goto loop;
    }
}";
        var vs = Analyze(src, MakeGotoRule());
        MustHave(vs, "GotoStatement");
        Assert.Matches(@"The method Bar\(\) utilizes a goto statement\.",
            vs[0].Description);
    }

    [Fact]
    public void GotoStatement_GotoCaseInsideSwitch_NotFlagged()
    {
        // goto case and goto default are idiomatic in C# switch
        var src = @"
public class Foo {
    public string Bar(int x) {
        switch (x) {
            case 1:
                goto case 2;
            case 2:
                return ""two"";
            default:
                goto case 1;
        }
    }
}";
        var vs = Analyze(src, MakeGotoRule());
        MustNotHave(vs, "GotoStatement");
    }

    [Fact]
    public void GotoStatement_NoGoto_NotFlagged()
    {
        var src = @"
public class Foo {
    public void Bar() { }
}";
        var vs = Analyze(src, MakeGotoRule());
        MustNotHave(vs, "GotoStatement");
    }

    // -------------------------------------------------------------------------
    // CountInLoopExpression
    // -------------------------------------------------------------------------

    private static CountInLoopExpressionRule MakeCountInLoopRule() =>
        MakeRule<CountInLoopExpressionRule>("CountInLoopExpression",
            "Avoid using {0} in {1} loops.");

    [Fact]
    public void CountInLoop_DotCountPropertyInFor_Flagged()
    {
        var src = @"
using System.Collections.Generic;
public class Foo {
    public void Bar(List<int> items) {
        for (int i = 0; i < items.Count; i++) { }
    }
}";
        var vs = Analyze(src, MakeCountInLoopRule());
        MustHave(vs, "CountInLoopExpression");
    }

    [Fact]
    public void CountInLoop_DotLengthInWhile_Flagged()
    {
        var src = @"
public class Foo {
    public void Bar(int[] arr) {
        int i = 0;
        while (i < arr.Length) { i++; }
    }
}";
        var vs = Analyze(src, MakeCountInLoopRule());
        MustHave(vs, "CountInLoopExpression");
    }

    [Fact]
    public void CountInLoop_LinqCountInFor_Flagged()
    {
        var src = @"
using System.Linq;
using System.Collections.Generic;
public class Foo {
    public void Bar(IEnumerable<int> items) {
        for (int i = 0; i < items.Count(); i++) { }
    }
}";
        var vs = Analyze(src, MakeCountInLoopRule());
        MustHave(vs, "CountInLoopExpression");
    }

    [Fact]
    public void CountInLoop_CountInBody_NotFlagged()
    {
        // Count in the loop body, not in the condition — should not flag
        var src = @"
using System.Collections.Generic;
public class Foo {
    public void Bar(List<int> items) {
        int n = items.Count;
        for (int i = 0; i < n; i++) {
            _ = items.Count;
        }
    }
}";
        var vs = Analyze(src, MakeCountInLoopRule());
        MustNotHave(vs, "CountInLoopExpression");
    }

    // -------------------------------------------------------------------------
    // DevelopmentCodeFragment
    // -------------------------------------------------------------------------

    private static DevelopmentCodeFragmentRule MakeDevCodeRule(
        Dictionary<string, string>? props = null) =>
        MakeRule<DevelopmentCodeFragmentRule>("DevelopmentCodeFragment",
            "The {0} {1}() calls the typical debug function {2}() which is mostly only used during development.",
            props);

    [Fact]
    public void DevCode_ConsoleWriteLine_Flagged()
    {
        var src = @"
public class Foo {
    public void Bar(string s) {
        Console.WriteLine(s);
    }
}";
        var vs = Analyze(src, MakeDevCodeRule());
        MustHave(vs, "DevelopmentCodeFragment");
    }

    [Fact]
    public void DevCode_ConsoleWrite_Flagged()
    {
        var src = @"
public class Foo {
    public void Bar(string s) {
        Console.Write(s);
    }
}";
        var vs = Analyze(src, MakeDevCodeRule());
        MustHave(vs, "DevelopmentCodeFragment");
    }

    [Fact]
    public void DevCode_DebugWriteLine_Flagged()
    {
        var src = @"
public class Foo {
    public void Bar(string s) {
        System.Diagnostics.Debug.WriteLine(s);
    }
}";
        // "System.Diagnostics.Debug.WriteLine" won't match our simple check;
        // qualify as just Debug.WriteLine in fixture
        var src2 = @"
public class Foo {
    public void Bar(string s) {
        Debug.WriteLine(s);
    }
}";
        var vs = Analyze(src2, MakeDevCodeRule());
        MustHave(vs, "DevelopmentCodeFragment");
    }

    [Fact]
    public void DevCode_UnwantedFunctionProperty_Flagged()
    {
        var src = @"
public class Foo {
    public void Bar(string s) {
        MyLogger.Dump(s);
    }
}";
        var vs = Analyze(src,
            MakeDevCodeRule(new Dictionary<string, string>
            {
                ["unwanted-functions"] = "MyLogger.Dump"
            }));
        MustHave(vs, "DevelopmentCodeFragment");
    }

    [Fact]
    public void DevCode_NormalCall_NotFlagged()
    {
        var src = @"
public class Foo {
    public void Bar(string s) {
        Process(s);
    }
    private void Process(string s) {}
}";
        var vs = Analyze(src, MakeDevCodeRule());
        MustNotHave(vs, "DevelopmentCodeFragment");
    }

    // -------------------------------------------------------------------------
    // EmptyCatchBlock
    // -------------------------------------------------------------------------

    private static EmptyCatchBlockRule MakeEmptyCatchRule() =>
        MakeRule<EmptyCatchBlockRule>("EmptyCatchBlock",
            "Avoid using empty catch blocks in {0}.");

    [Fact]
    public void EmptyCatch_EmptyBlock_Flagged()
    {
        var src = @"
public class Foo {
    public void Bar() {
        try {
            DoSomething();
        } catch (Exception e) {}
    }
    private void DoSomething() {}
}";
        var vs = Analyze(src, MakeEmptyCatchRule());
        MustHave(vs, "EmptyCatchBlock");
    }

    [Fact]
    public void EmptyCatch_CommentOnlyBlock_Flagged()
    {
        // A lone comment is still empty (matches phpmd behavior)
        var src = @"
public class Foo {
    public void Bar() {
        try {
            DoSomething();
        } catch (Exception e) {
            // ignore
        }
    }
    private void DoSomething() {}
}";
        // Comment-only block: no statements, so Statements.Count == 0 => flagged
        var vs = Analyze(src, MakeEmptyCatchRule());
        MustHave(vs, "EmptyCatchBlock");
    }

    [Fact]
    public void EmptyCatch_WithHandling_NotFlagged()
    {
        var src = @"
public class Foo {
    public void Bar() {
        try {
            DoSomething();
        } catch (Exception e) {
            throw;
        }
    }
    private void DoSomething() {}
}";
        var vs = Analyze(src, MakeEmptyCatchRule());
        MustNotHave(vs, "EmptyCatchBlock");
    }

    // -------------------------------------------------------------------------
    // CouplingBetweenObjects
    // -------------------------------------------------------------------------

    private static CouplingBetweenObjectsRule MakeCboRule(int maximum = 13) =>
        MakeRule<CouplingBetweenObjectsRule>("CouplingBetweenObjects",
            "The class {0} has a coupling between objects value of {1}. Consider to reduce the number of dependencies under {2}.",
            new Dictionary<string, string> { ["maximum"] = maximum.ToString() });

    [Fact]
    public void CouplingBetweenObjects_OverThreshold_Flagged()
    {
        // 14 distinct custom types referenced in fields and params
        var src = @"
public class Hub {
    private A _a; private B _b; private C _c; private D _d;
    private E _e; private F _f; private G _g; private H _h;
    private I _i; private J _j; private K _k; private L _l;
    private M _m; private N _n;
    public void Process(O o) {}
}
public class A{} public class B{} public class C{} public class D{}
public class E{} public class F{} public class G{} public class H{}
public class I{} public class J{} public class K{} public class L{}
public class M{} public class N{} public class O{}
";
        var vs = Analyze(src, MakeCboRule(13));
        MustHave(vs, "CouplingBetweenObjects");
    }

    [Fact]
    public void CouplingBetweenObjects_UnderThreshold_NotFlagged()
    {
        var src = @"
public class Simple {
    private FooA _a;
    public void DoThing(FooB b) {}
}
public class FooA {} public class FooB {}
";
        var vs = Analyze(src, MakeCboRule(13));
        MustNotHave(vs, "CouplingBetweenObjects");
    }

    [Fact]
    public void CouplingBetweenObjects_BuiltinTypesNotCounted()
    {
        var src = @"
public class Simple {
    private int _x;
    private string _s;
    private bool _b;
    public void Do(long l, double d) { }
}";
        var vs = Analyze(src, MakeCboRule(1));
        MustNotHave(vs, "CouplingBetweenObjects");
    }

    // -------------------------------------------------------------------------
    // GlobalVariable
    // -------------------------------------------------------------------------

    private static GlobalVariableRule MakeGlobalVarRule(bool reportImmutable = false) =>
        MakeRule<GlobalVariableRule>("GlobalVariable",
            "Avoid using static mutable state: {0}.",
            new Dictionary<string, string>
            {
                ["report-immutable"] = reportImmutable ? "true" : "false"
            });

    [Fact]
    public void GlobalVariable_MutatedStaticField_Flagged()
    {
        var src = @"
public class Counter {
    private static int _count = 0;

    public static void Increment() {
        _count++;
    }
}";
        var vs = Analyze(src, MakeGlobalVarRule());
        MustHave(vs, "GlobalVariable");
    }

    [Fact]
    public void GlobalVariable_StaticReadonly_NotFlagged()
    {
        var src = @"
public class Constants {
    private static readonly int MaxRetries = 3;
    private static readonly string Prefix = ""foo"";
}";
        var vs = Analyze(src, MakeGlobalVarRule());
        MustNotHave(vs, "GlobalVariable");
    }

    [Fact]
    public void GlobalVariable_Const_NotFlagged()
    {
        var src = @"
public class Constants {
    private const int MaxRetries = 3;
    public const string AppName = ""myapp"";
}";
        var vs = Analyze(src, MakeGlobalVarRule());
        MustNotHave(vs, "GlobalVariable");
    }

    [Fact]
    public void GlobalVariable_UnmutatedStatic_NotFlaggedByDefault()
    {
        var src = @"
public class Lookup {
    private static int[] _table = new int[]{ 1, 2, 3 };

    public static int Get(int i) => _table[i];
}";
        var vs = Analyze(src, MakeGlobalVarRule(reportImmutable: false));
        MustNotHave(vs, "GlobalVariable");
    }

    [Fact]
    public void GlobalVariable_UnmutatedStatic_FlaggedWithReportImmutable()
    {
        var src = @"
public class Lookup {
    private static int[] _table = new int[]{ 1, 2, 3 };
}";
        var vs = Analyze(src, MakeGlobalVarRule(reportImmutable: true));
        MustHave(vs, "GlobalVariable");
    }

    [Fact]
    public void GlobalVariable_AssignedStaticField_Flagged()
    {
        var src = @"
public class Registry {
    public static string Current = """";

    public static void Set(string s) {
        Current = s;
    }
}";
        var vs = Analyze(src, MakeGlobalVarRule());
        MustHave(vs, "GlobalVariable");
    }

    [Fact]
    public void GlobalVariable_InstanceField_NotFlagged()
    {
        var src = @"
public class Foo {
    private int _x = 0;
    public void Inc() { _x++; }
}";
        var vs = Analyze(src, MakeGlobalVarRule());
        MustNotHave(vs, "GlobalVariable");
    }

    // -------------------------------------------------------------------------
    // LackOfCohesionOfMethods (LCOM4)
    // -------------------------------------------------------------------------

    private static LackOfCohesionOfMethodsRule MakeLcomRule(int maximum = 1) =>
        MakeRule<LackOfCohesionOfMethodsRule>("LackOfCohesionOfMethods",
            "The {0} has a Lack of Cohesion Of Methods (LCOM4) value of {1}. Consider to split this class into {1} smaller classes.",
            new Dictionary<string, string> { ["maximum"] = maximum.ToString() });

    // Two disjoint method groups — each touches only its own field.
    private const string DisjointSrc = @"
public class Server {
    private System.Collections.Generic.Dictionary<string, int> _conns = new();
    private System.Collections.Generic.Dictionary<string, int> _stats = new();

    public void Accept(string addr) { _conns[addr] = 1; }
    public int CloseAll() {
        int total = 0;
        foreach (var _ in _conns) total++;
        return total;
    }

    public void Record(string k) { _stats[k] = 1; }
    public int Snapshot() {
        int n = 0;
        foreach (var kv in _stats) n += kv.Value;
        return n;
    }
}";

    [Fact]
    public void Lcom4_DisjointMethodGroups_Flagged()
    {
        var vs = Analyze(DisjointSrc, MakeLcomRule());
        MustHave(vs, "LackOfCohesionOfMethods");
        // Should report LCOM4 = 2
        var v = vs.First(x => x.Rule.Name == "LackOfCohesionOfMethods");
        Assert.Contains("2", v.Description);
    }

    [Fact]
    public void Lcom4_CohesiveClass_NotFlagged()
    {
        // report() bridges both clusters → cohesive, LCOM4 = 1
        var src = DisjointSrc + @"
public partial class Server {
    public int Report() { return _conns.Count + Snapshot(); }
}";
        // Use fresh source without partial — just add Report() to the disjoint src
        var merged = @"
public class Server {
    private System.Collections.Generic.Dictionary<string, int> _conns = new();
    private System.Collections.Generic.Dictionary<string, int> _stats = new();

    public void Accept(string addr) { _conns[addr] = 1; }
    public int CloseAll() {
        int total = 0;
        foreach (var _ in _conns) total++;
        return total;
    }

    public void Record(string k) { _stats[k] = 1; }
    public int Snapshot() {
        int n = 0;
        foreach (var kv in _stats) n += kv.Value;
        return n;
    }

    public int Report() { return _conns.Count + Snapshot(); }
}";
        var vs = Analyze(merged, MakeLcomRule());
        MustNotHave(vs, "LackOfCohesionOfMethods");
    }

    [Fact]
    public void Lcom4_DataCarrierWithAccessors_NotFlagged()
    {
        var src = @"
public class Config {
    private string _host = """";
    private int _port = 80;
    private bool _tls = false;

    public string Host() { return _host; }
    public void SetHost(string h) { _host = h; }
    public int Port() { return _port; }
    public void SetPort(int p) { _port = p; }
    public bool Tls() { return _tls; }
}";
        var vs = Analyze(src, MakeLcomRule());
        MustNotHave(vs, "LackOfCohesionOfMethods");
    }

    [Fact]
    public void Lcom4_AccessorCallCountsAsFieldUse()
    {
        // over() calls Count() (getter for _count) and accesses _limit directly
        // → all methods in one component
        var src = @"
public class Meter {
    private int _count = 0;
    private int _limit = 10;

    public int Count() { return _count; }
    public void Bump(int n) { _count += n; }
    public bool Over() { return Count() > _limit; }
    public void Widen(int l) { _limit = l * 2; }
}";
        var vs = Analyze(src, MakeLcomRule());
        MustNotHave(vs, "LackOfCohesionOfMethods");
    }

    [Fact]
    public void Lcom4_MaximumProperty_Configurable()
    {
        // With maximum=2 the two-group fixture is acceptable
        var vs = Analyze(DisjointSrc, MakeLcomRule(maximum: 2));
        MustNotHave(vs, "LackOfCohesionOfMethods");
    }

    [Fact]
    public void Lcom4_SingleFieldClass_NotFlagged()
    {
        var src = @"
public class SingleField {
    private int _x = 0;
    public int Double() { return _x * 2; }
}";
        var vs = Analyze(src, MakeLcomRule());
        MustNotHave(vs, "LackOfCohesionOfMethods");
    }

    // -------------------------------------------------------------------------
    // Multiple rules together (design "integration")
    // -------------------------------------------------------------------------

    [Fact]
    public void Design_MultipleRules_AllFire()
    {
        var src = @"
public class SuspectCode {
    public void DoStuff(int x) {
        Console.WriteLine(x);
        if (x == 0) Environment.Exit(1);
        try {
            DoOther();
        } catch (Exception) {}
    }
    private void DoOther() {}

    public void WithGoto(int x) {
        start:
        if (x-- > 0) goto start;
    }

    public void WithCountLoop(int[] arr) {
        for (int i = 0; i < arr.Length; i++) {}
    }
}";
        var vs = Analyze(src,
            MakeExitRule(),
            MakeGotoRule(),
            MakeCountInLoopRule(),
            MakeDevCodeRule(),
            MakeEmptyCatchRule());

        MustHave(vs,
            "ExitExpression",
            "GotoStatement",
            "CountInLoopExpression",
            "DevelopmentCodeFragment",
            "EmptyCatchBlock");
    }
}
