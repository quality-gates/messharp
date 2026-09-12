using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.Rules.UnusedCode;
using Xunit;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.Tests;

/// <summary>
/// Behavioral tests for the UnusedCode ruleset.
/// Pattern: MustHave / MustNotHave helpers over crafted fixture sources.
/// </summary>
public class UnusedCodeRulesTests
{
    // ─── helpers ────────────────────────────────────────────────────────────

    private static List<Violation> Analyze(string source)
    {
        var sf = ModelBuilder.Parse("fixture.cs", source);
        var set = MakeSet();
        return Engine.Analyze(sf, new[] { set });
    }

    private static RuleSetType MakeSet()
    {
        var rules = new List<BaseRule>
        {
            MakeRule<UnusedPrivateFieldRule>(
                "UnusedPrivateField",
                "Avoid unused private fields such as '{0}'."),
            MakeRule<UnusedLocalVariableRule>(
                "UnusedLocalVariable",
                "Avoid unused local variables such as '{0}'."),
            MakeRule<UnusedPrivateMethodRule>(
                "UnusedPrivateMethod",
                "Avoid unused private methods such as '{0}'."),
            MakeRule<UnusedFormalParameterRule>(
                "UnusedFormalParameter",
                "Avoid unused parameters such as '{0}'."),
        };
        var set = new RuleSetType { Name = "unusedcode" };
        foreach (var r in rules) set.Rules.Add(r);
        return set;
    }

    private static T MakeRule<T>(string name, string message,
        Dictionary<string, string>? props = null) where T : BaseRule, new()
    {
        return new T
        {
            Name = name,
            Message = message,
            Priority = 3,
            SetName = "unusedcode",
            ExternalUrl = "",
            Description = "",
            Since = "0.2",
            RuleProps = props is null ? Properties.Empty : new Properties(props),
        };
    }

    private static void MustHave(List<Violation> violations, params string[] ruleNames)
    {
        foreach (var name in ruleNames)
        {
            Assert.True(
                violations.Any(v => v.Rule.Name == name),
                $"Expected violation '{name}' not found. Found: {string.Join(", ", violations.Select(v => v.Rule.Name))}");
        }
    }

    private static void MustNotHave(List<Violation> violations, params string[] ruleNames)
    {
        foreach (var name in ruleNames)
        {
            Assert.False(
                violations.Any(v => v.Rule.Name == name),
                $"Unexpected violation '{name}' was reported.");
        }
    }

    // ─── UnusedPrivateField ─────────────────────────────────────────────────

    [Fact]
    public void UnusedPrivateField_UnusedField_Fires()
    {
        var src = @"
public class Foo
{
    private int _unused;
    private int _used;
    public int GetUsed() { return _used; }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedPrivateField");
        Assert.Contains(vs, v => v.Rule.Name == "UnusedPrivateField"
            && v.Description.Contains("_unused"));
    }

    [Fact]
    public void UnusedPrivateField_UsedField_NoFire()
    {
        var src = @"
public class Foo
{
    private int _x;
    public int Get() { return _x; }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateField");
    }

    [Fact]
    public void UnusedPrivateField_WriteOnlyAssignment_Fires()
    {
        var src = @"
class C
{
    private int x;
    public void Set() { x = 1; }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedPrivateField");
    }

    [Fact]
    public void UnusedPrivateField_WriteOnlyMemberAssignment_Fires()
    {
        var src = @"
class C
{
    private int x;
    public void Set() { this.x = 1; }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedPrivateField");
    }

    [Fact]
    public void UnusedPrivateField_CompoundAssignmentReadsField_NoFire()
    {
        var src = @"
class C
{
    private int x;
    public void Increment() { x += 1; }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateField");
    }

    [Fact]
    public void UnusedPrivateField_ObjectInitializerKeyCountsAsUse_NoFire()
    {
        var src = @"
class C
{
    private int x;
    public C Create() { return new C { x = 1 }; }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateField");
    }

    [Fact]
    public void UnusedPrivateField_UsedViaMemberAccess_NoFire()
    {
        // this._x counts as a use
        var src = @"
public class Foo
{
    private int _x;
    public int Get() { return this._x; }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateField");
    }

    [Fact]
    public void UnusedPrivateField_UsedViaNameof_NoFire()
    {
        var src = @"
public class Foo
{
    private int _x;
    public string Name() { return nameof(_x); }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateField");
    }

    [Fact]
    public void UnusedPrivateField_PublicField_NotChecked()
    {
        // Public fields are not private → should not fire UnusedPrivateField
        var src = @"
public class Foo
{
    public int Unused;
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateField");
    }

    [Fact]
    public void UnusedPrivateField_ExactMessage()
    {
        var src = @"
public class Foo
{
    private int _dead;
}";
        var vs = Analyze(src);
        var v = Assert.Single(vs.Where(v => v.Rule.Name == "UnusedPrivateField"));
        Assert.Equal("Avoid unused private fields such as '_dead'.", v.Description);
    }

    [Fact]
    public void UnusedPrivateField_ReportsFieldLine_NotClassLine()
    {
        var src = @"
public class Foo
{
    private int _dead;
}";
        var vs = Analyze(src);
        var v = Assert.Single(vs.Where(v => v.Rule.Name == "UnusedPrivateField"));
        Assert.Equal(4, v.BeginLine);
    }

    [Fact]
    public void UnusedPrivateField_NestedClass_Fires()
    {
        var src = @"
public class Outer
{
    public class Inner
    {
        private int _dead;
    }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedPrivateField");
        var v = Assert.Single(vs.Where(v => v.Rule.Name == "UnusedPrivateField"));
        Assert.Contains("_dead", v.Description);
        Assert.Equal(6, v.BeginLine);
        Assert.Equal("Inner", v.Class);
    }

    // ─── shadowing (issue #90) ───────────────────────────────────────────────

    [Fact]
    public void UnusedPrivateField_ParameterShadowing_Fires()
    {
        var src = @"
public class FieldShadowing
{
    private int value;
    public void Use(int value)
    {
        System.Console.WriteLine(value);
    }
}";
        var vs = Analyze(src);
        var v = Assert.Single(vs.Where(v => v.Rule.Name == "UnusedPrivateField"));
        Assert.Contains("value", v.Description);
    }

    [Fact]
    public void UnusedPrivateField_LocalShadowing_Fires()
    {
        var src = @"
public class FieldShadowing
{
    private int value;
    public void Use()
    {
        int value = 5;
        System.Console.WriteLine(value);
    }
}";
        var vs = Analyze(src);
        var v = Assert.Single(vs.Where(v => v.Rule.Name == "UnusedPrivateField"));
        Assert.Contains("value", v.Description);
    }

    [Fact]
    public void UnusedPrivateField_OtherInstanceAccess_NoFire()
    {
        // Reading a private field through another instance of the same class
        // is legal C# and must still count as a use.
        var src = @"
public class FieldShadowing
{
    private int value;
    public int ReadOther(FieldShadowing other)
    {
        return other.value;
    }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateField");
    }

    [Fact]
    public void UnusedPrivateMethod_ParameterShadowing_Fires()
    {
        var src = @"
public class MethodShadowing
{
    private void Helper() {}
    public void Use(int Helper)
    {
        System.Console.WriteLine(Helper);
    }
}";
        var vs = Analyze(src);
        var v = Assert.Single(vs.Where(v => v.Rule.Name == "UnusedPrivateMethod"));
        Assert.Contains("Helper", v.Description);
    }

    // ─── UnusedLocalVariable ────────────────────────────────────────────────

    [Fact]
    public void UnusedLocalVariable_UnusedLocal_Fires()
    {
        var src = @"
public class Foo
{
    public void Bar()
    {
        int unused = 5;
    }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedLocalVariable");
        Assert.Contains(vs, v => v.Rule.Name == "UnusedLocalVariable"
            && v.Description.Contains("unused"));
    }

    [Fact]
    public void UnusedLocalVariable_DeclarationPattern_Fires()
    {
        var src = @"
class C
{
    public void M()
    {
        if (new object() is int unused) { }
    }
}";
        var vs = Analyze(src);
        Assert.Contains(vs, v => v.Rule.Name == "UnusedLocalVariable"
            && v.BeginLine == 6
            && v.Description.Contains("unused"));
    }

    [Fact]
    public void UnusedLocalVariable_DeconstructionLocals_FiresIncludingNestedNames()
    {
        var src = @"
public class C
{
    public void M()
    {
        var (x, (y, _)) = (1, (2, 3));
    }
}";
        var violations = Analyze(src)
            .Where(v => v.Rule.Name == "UnusedLocalVariable")
            .ToList();

        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, v => v.Description.Contains("x") && v.BeginLine == 6);
        Assert.Contains(violations, v => v.Description.Contains("y") && v.BeginLine == 6);
    }

    [Fact]
    public void UnusedLocalVariable_ReadDeclarationPattern_NoFire()
    {
        var src = @"
class C
{
    public void M()
    {
        if (new object() is int value) { _ = value; }
    }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedLocalVariable");
    }

    [Fact]
    public void UnusedLocalVariable_DeclarationPatternDiscard_NoFire()
    {
        var src = @"
class C
{
    public void M()
    {
        if (new object() is int _) { }
    }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedLocalVariable");
    }

    [Fact]
    public void UnusedLocalVariable_UsedLocal_NoFire()
    {
        var src = @"
public class Foo
{
    public int Bar()
    {
        int x = 5;
        return x;
    }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedLocalVariable");
    }

    [Fact]
    public void UnusedLocalVariable_Discard_NoFire()
    {
        // `_` is an explicit discard; should never be reported
        var src = @"
public class Foo
{
    public void Bar(int x)
    {
        _ = x;
    }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedLocalVariable");
    }

    [Fact]
    public void UnusedLocalVariable_WriteOnlyLocal_Fires()
    {
        // assigned but never read
        var src = @"
public class Foo
{
    public void Bar()
    {
        int writeOnly = 0;
        writeOnly = 5;
    }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedLocalVariable");
    }

    [Fact]
    public void UnusedLocalVariable_ExceptionsProperty_Suppresses()
    {
        var src = @"
public class Foo
{
    public void Bar()
    {
        int ignored = 5;
    }
}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var rule = MakeRule<UnusedLocalVariableRule>(
            "UnusedLocalVariable",
            "Avoid unused local variables such as '{0}'.",
            new Dictionary<string, string> { ["exceptions"] = "ignored" });
        var set = new RuleSetType { Name = "unusedcode" };
        set.Rules.Add(rule);
        var vs = Engine.Analyze(sf, new[] { set });
        MustNotHave(vs, "UnusedLocalVariable");
    }

    [Fact]
    public void UnusedLocalVariable_OutVar_Used_NoFire()
    {
        var src = @"
public class Foo
{
    public void Bar()
    {
        if (int.TryParse(""1"", out var n))
        {
            _ = n;
        }
    }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedLocalVariable");
    }

    [Fact]
    public void UnusedLocalVariable_ExactMessage()
    {
        var src = @"
public class Foo
{
    public void Bar()
    {
        int deadVar = 0;
    }
}";
        var vs = Analyze(src);
        var v = Assert.Single(vs.Where(v => v.Rule.Name == "UnusedLocalVariable"));
        Assert.Equal("Avoid unused local variables such as 'deadVar'.", v.Description);
    }

    [Fact]
    public void UnusedLocalVariable_ForLoopUnusedVar_Fires()
    {
        var src = @"
public class Foo
{
    public void Bar()
    {
        for (int unused = 0, i = 0; i < 10; i++)
        {
        }
    }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedLocalVariable");
        Assert.Contains(vs, v => v.Rule.Name == "UnusedLocalVariable"
            && v.Description.Contains("unused"));
    }

    [Fact]
    public void UnusedLocalVariable_UsingStatementUnusedVar_Fires()
    {
        var src = @"
public class Foo
{
    public void Bar()
    {
        using (var unused = new System.IO.MemoryStream())
        {
        }
    }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedLocalVariable");
        Assert.Contains(vs, v => v.Rule.Name == "UnusedLocalVariable"
            && v.Description.Contains("unused"));
    }

    [Fact]
    public void UnusedLocalVariable_FixedStatementUnusedVar_Fires()
    {
        var src = @"
public class Foo
{
    public unsafe void Bar()
    {
        int[] arr = new int[5];
        fixed (int* unused = arr)
        {
        }
    }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedLocalVariable");
        Assert.Contains(vs, v => v.Rule.Name == "UnusedLocalVariable"
            && v.Description.Contains("unused"));
    }

    // ─── UnusedPrivateMethod ─────────────────────────────────────────────────

    [Fact]
    public void UnusedPrivateMethod_ExplicitInterfaceImplementation_NotChecked()
    {
        var src = @"
public interface IWorker
{
    void Run(bool flag);
}

public class Worker : IWorker
{
    void IWorker.Run(bool flag) {}
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateMethod");
    }

    [Fact]
    public void UnusedPrivateMethod_QualifiedExplicitInterfaceImplementation_NotChecked()
    {
        var src = @"
namespace Contracts
{
    public interface IWorker
    {
        void Run();
    }
}

public class Worker : Contracts.IWorker
{
    void Contracts.IWorker.Run() {}
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateMethod");
    }

    [Fact]
    public void UnusedPrivateMethod_GenericExplicitInterfaceImplementation_NotChecked()
    {
        var src = @"
public interface IWorker<T>
{
    void Run();
}

public class Worker : IWorker<string>
{
    void IWorker<string>.Run() {}
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateMethod");
    }

    [Fact]
    public void UnusedPrivateMethod_UnusedMethod_Fires()
    {
        var src = @"
public class Foo
{
    private void Dead() {}
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedPrivateMethod");
        Assert.Contains(vs, v => v.Rule.Name == "UnusedPrivateMethod"
            && v.Description.Contains("Dead"));
    }

    [Fact]
    public void UnusedPrivateMethod_UsedMethod_NoFire()
    {
        var src = @"
public class Foo
{
    private int Helper() { return 1; }
    public int Run() { return Helper(); }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateMethod");
    }

    [Fact]
    public void UnusedPrivateMethod_UsedAsMethodGroup_NoFire()
    {
        var src = @"
using System;
public class Foo
{
    private int Compute() { return 1; }
    public Func<int> Get() { return Compute; }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateMethod");
    }

    [Fact]
    public void UnusedPrivateMethod_PublicMethod_NotChecked()
    {
        var src = @"
public class Foo
{
    public void Dead() {}
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateMethod");
    }

    [Fact]
    public void UnusedPrivateMethod_Constructor_NotChecked()
    {
        // Private constructors should not be flagged as unused methods
        var src = @"
public class Foo
{
    private Foo() {}
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedPrivateMethod");
    }

    [Fact]
    public void UnusedPrivateMethod_NestedClass_Fires()
    {
        var src = @"
public class Outer
{
    public class Inner
    {
        private void Dead() {}
    }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedPrivateMethod");
        var v = Assert.Single(vs.Where(v => v.Rule.Name == "UnusedPrivateMethod"));
        Assert.Contains("Dead", v.Description);
        Assert.Equal(6, v.BeginLine);
        Assert.Equal("Inner", v.Class);
    }

    // ─── UnusedFormalParameter ───────────────────────────────────────────────

    [Fact]
    public void UnusedFormalParameter_UnusedParam_Fires()
    {
        var src = @"
public class Foo
{
    private void Bar(int unused)
    {
    }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedFormalParameter");
        Assert.Contains(vs, v => v.Rule.Name == "UnusedFormalParameter"
            && v.Description.Contains("unused"));
    }

    [Fact]
    public void UnusedFormalParameter_UsedParam_NoFire()
    {
        var src = @"
public class Foo
{
    private int Add(int a, int b) { return a + b; }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedFormalParameter");
    }

    [Fact]
    public void UnusedFormalParameter_CompoundAssignmentReadsParam_NoFire()
    {
        var src = @"
public class Worker
{
    public void Accumulate(int count)
    {
        count += 1;
    }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedFormalParameter");
    }

    [Fact]
    public void UnusedFormalParameter_DiscardParam_NoFire()
    {
        // `_` is an explicit discard parameter
        var src = @"
public class Foo
{
    private void Bar(int _) { }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedFormalParameter");
    }

    [Fact]
    public void UnusedFormalParameter_UsedInExpressionBody_NoFire()
    {
        var src = @"
public class Foo
{
    private int Double(int x) => x * 2;
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedFormalParameter");
    }

    [Fact]
    public void UnusedFormalParameter_AbstractMethod_NoFire()
    {
        // Abstract methods have no body — should not fire
        var src = @"
public abstract class Foo
{
    public abstract void Bar(int x);
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedFormalParameter");
    }

    [Fact]
    public void UnusedFormalParameter_OutParameterAssigned_NoFire()
    {
        var src = @"
public class Foo
{
    public bool TryParse(string s, out int val)
    {
        val = 1;
        return s.Length > 0;
    }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "UnusedFormalParameter");
    }

    [Fact]
    public void UnusedFormalParameter_OutParameterUnassigned_Fires()
    {
        var src = @"
public class Foo
{
    public bool TryParse(string s, out int val)
    {
        return s.Length > 0;
    }
}";
        var vs = Analyze(src);
        MustHave(vs, "UnusedFormalParameter");
    }

    // ─── combined fixture ────────────────────────────────────────────────────

    [Fact]
    public void AllFourRules_FireOnCombinedFixture()
    {
        var src = @"
public class Widget
{
    private int _dead;
    private int _used;

    public int GetUsed() { return _used; }

    private void DeadMethod() {}

    private void Live(int spare)
    {
        int writeOnly = 0;
        writeOnly = 5;
    }
}";
        var vs = Analyze(src);
        MustHave(vs,
            "UnusedPrivateField",
            "UnusedPrivateMethod",
            "UnusedFormalParameter",
            "UnusedLocalVariable");
    }
}
