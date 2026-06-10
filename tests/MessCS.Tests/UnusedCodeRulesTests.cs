using MessCS.Model;
using MessCS.Rule;
using MessCS.Rules.UnusedCode;
using Xunit;
using RuleSetType = MessCS.Rule.RuleSet;

namespace MessCS.Tests;

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

    // ─── UnusedPrivateMethod ─────────────────────────────────────────────────

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
