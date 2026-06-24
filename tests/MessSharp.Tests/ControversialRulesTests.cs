using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.Rules.Controversial;
using Xunit;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.Tests;

/// <summary>
/// Behavioral tests for the Controversial ruleset (C# naming convention checks).
/// PascalCase for classes/methods/properties; camelCase for parameters/locals.
/// </summary>
public class ControversialRulesTests
{
    // ─── helpers ─────────────────────────────────────────────────────────────

    private static List<Violation> Analyze(string source,
        Dictionary<string, string>? props = null)
    {
        var sf = ModelBuilder.Parse("fixture.cs", source);
        var set = MakeSet(props);
        return Engine.Analyze(sf, new[] { set });
    }

    private static RuleSetType MakeSet(Dictionary<string, string>? sharedProps = null)
    {
        var rules = new List<BaseRule>
        {
            MakeRule<CamelCaseClassNameRule>(
                "CamelCaseClassName",
                "The class {0} is not named in PascalCase.",
                sharedProps),
            MakeRule<CamelCaseMethodNameRule>(
                "CamelCaseMethodName",
                "The method {0} is not named in PascalCase.",
                sharedProps),
            MakeRule<CamelCasePropertyNameRule>(
                "CamelCasePropertyName",
                "The property {0} is not named in camelCase.",
                sharedProps),
            MakeRule<CamelCaseParameterNameRule>(
                "CamelCaseParameterName",
                "The parameter {0} is not named in camelCase.",
                sharedProps),
            MakeRule<CamelCaseVariableNameRule>(
                "CamelCaseVariableName",
                "The variable {0} is not named in camelCase.",
                sharedProps),
        };
        var set = new RuleSetType { Name = "controversial" };
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
            Priority = 1,
            SetName = "controversial",
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

    // ─── CamelCaseClassName ─────────────────────────────────────────────────

    [Fact]
    public void CamelCaseClassName_SnakeCaseName_Fires()
    {
        var src = @"public class bad_name { }";
        var vs = Analyze(src);
        MustHave(vs, "CamelCaseClassName");
        Assert.Contains(vs, v => v.Rule.Name == "CamelCaseClassName"
            && v.Description.Contains("bad_name"));
    }

    [Fact]
    public void CamelCaseClassName_PascalCase_NoFire()
    {
        var src = @"public class GoodName { }";
        var vs = Analyze(src);
        MustNotHave(vs, "CamelCaseClassName");
    }

    [Fact]
    public void CamelCaseClassName_Interface_SnakeCase_Fires()
    {
        var src = @"public interface bad_interface { }";
        var vs = Analyze(src);
        MustHave(vs, "CamelCaseClassName");
    }

    [Fact]
    public void CamelCaseClassName_Interface_PascalCase_NoFire()
    {
        var src = @"public interface IGoodName { }";
        var vs = Analyze(src);
        MustNotHave(vs, "CamelCaseClassName");
    }

    [Fact]
    public void CamelCaseClassName_ExactMessage()
    {
        var src = @"public class my_class { }";
        var vs = Analyze(src);
        var v = Assert.Single(vs.Where(v => v.Rule.Name == "CamelCaseClassName"));
        Assert.Equal("The class my_class is not named in PascalCase.", v.Description);
    }

    // ─── CamelCaseMethodName ─────────────────────────────────────────────────

    [Fact]
    public void CamelCaseMethodName_SnakeCaseMethod_Fires()
    {
        var src = @"
public class Foo
{
    public void get_name() {}
}";
        var vs = Analyze(src);
        MustHave(vs, "CamelCaseMethodName");
        Assert.Contains(vs, v => v.Rule.Name == "CamelCaseMethodName"
            && v.Description.Contains("get_name"));
    }

    [Fact]
    public void CamelCaseMethodName_PascalCase_NoFire()
    {
        var src = @"
public class Foo
{
    public void GetName() {}
}";
        var vs = Analyze(src);
        MustNotHave(vs, "CamelCaseMethodName");
    }

    [Fact]
    public void CamelCaseMethodName_Constructor_NoFire()
    {
        // Constructors are not checked (name is forced by the class name)
        var src = @"public class Foo { public Foo() {} }";
        var vs = Analyze(src);
        MustNotHave(vs, "CamelCaseMethodName");
    }

    [Fact]
    public void CamelCaseMethodName_ExactMessage()
    {
        var src = @"
public class Foo
{
    public void snake_method() {}
}";
        var vs = Analyze(src);
        var v = Assert.Single(vs.Where(v => v.Rule.Name == "CamelCaseMethodName"));
        Assert.Equal("The method snake_method is not named in PascalCase.", v.Description);
    }

    // ─── CamelCasePropertyName ────────────────────────────────────────────────

    [Fact]
    public void CamelCasePropertyName_SnakeCasePrivateField_Fires()
    {
        var src = @"
public class Foo
{
    private int bad_field;
}";
        var vs = Analyze(src);
        MustHave(vs, "CamelCasePropertyName");
        Assert.Contains(vs, v => v.Rule.Name == "CamelCasePropertyName"
            && v.Description.Contains("bad_field"));
    }

    [Fact]
    public void CamelCasePropertyName_CamelCasePrivateField_NoFire()
    {
        var src = @"
public class Foo
{
    private int goodField;
}";
        var vs = Analyze(src);
        MustNotHave(vs, "CamelCasePropertyName");
    }

    [Fact]
    public void CamelCasePropertyName_UnderscorePrefixPrivateField_NoFire()
    {
        // _camelCase is OK by default (allowUnderscorePrefix=true)
        var src = @"
public class Foo
{
    private int _goodField;
}";
        var vs = Analyze(src);
        MustNotHave(vs, "CamelCasePropertyName");
    }

    [Fact]
    public void CamelCasePropertyName_UnderscorePrefixDisabled_Fires()
    {
        var src = @"
public class Foo
{
    private int _badNow;
}";
        var props = new Dictionary<string, string> { ["allowUnderscorePrefix"] = "false" };
        var vs = Analyze(src, props);
        MustHave(vs, "CamelCasePropertyName");
    }

    [Fact]
    public void CamelCasePropertyName_PublicField_SnakeCase_Fires()
    {
        // Public fields must be PascalCase
        var src = @"
public class Foo
{
    public int bad_public;
}";
        var vs = Analyze(src);
        MustHave(vs, "CamelCasePropertyName");
    }

    [Fact]
    public void CamelCasePropertyName_PublicField_PascalCase_NoFire()
    {
        var src = @"
public class Foo
{
    public int GoodPublic;
}";
        var vs = Analyze(src);
        MustNotHave(vs, "CamelCasePropertyName");
    }

    // ─── CamelCaseParameterName ───────────────────────────────────────────────

    [Fact]
    public void CamelCaseParameterName_SnakeCaseParam_Fires()
    {
        var src = @"
public class Foo
{
    public void Do(int bad_param) {}
}";
        var vs = Analyze(src);
        MustHave(vs, "CamelCaseParameterName");
        Assert.Contains(vs, v => v.Rule.Name == "CamelCaseParameterName"
            && v.Description.Contains("bad_param"));
    }

    [Fact]
    public void CamelCaseParameterName_CamelCase_NoFire()
    {
        var src = @"
public class Foo
{
    public void Do(int goodParam) {}
}";
        var vs = Analyze(src);
        MustNotHave(vs, "CamelCaseParameterName");
    }

    [Fact]
    public void CamelCaseParameterName_DiscardParam_NoFire()
    {
        var src = @"
public class Foo
{
    public void Do(int _) {}
}";
        var vs = Analyze(src);
        MustNotHave(vs, "CamelCaseParameterName");
    }

    [Fact]
    public void CamelCaseParameterName_ExactMessage()
    {
        var src = @"
public class Foo
{
    public void Do(int under_score) {}
}";
        var vs = Analyze(src);
        var v = Assert.Single(vs.Where(v => v.Rule.Name == "CamelCaseParameterName"));
        Assert.Equal("The parameter under_score is not named in camelCase.", v.Description);
    }

    // ─── CamelCaseVariableName ────────────────────────────────────────────────

    [Fact]
    public void CamelCaseVariableName_SnakeCaseLocal_Fires()
    {
        var src = @"
public class Foo
{
    public void Bar()
    {
        int bad_var = 1;
        _ = bad_var;
    }
}";
        var vs = Analyze(src);
        MustHave(vs, "CamelCaseVariableName");
        Assert.Contains(vs, v => v.Rule.Name == "CamelCaseVariableName"
            && v.Description.Contains("bad_var"));
    }

    [Fact]
    public void CamelCaseVariableName_CamelCase_NoFire()
    {
        var src = @"
public class Foo
{
    public int Bar()
    {
        int goodVar = 1;
        return goodVar;
    }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "CamelCaseVariableName");
    }

    [Fact]
    public void CamelCaseVariableName_UnderscorePrefixLocal_NoFire()
    {
        // _camelCase locals allowed by default
        var src = @"
public class Foo
{
    public int Bar()
    {
        int _goodLocal = 1;
        return _goodLocal;
    }
}";
        var vs = Analyze(src);
        MustNotHave(vs, "CamelCaseVariableName");
    }

    [Fact]
    public void CamelCaseVariableName_ReportedOnlyOnce()
    {
        // Same name used in multiple assignments — reported once per method
        var src = @"
public class Foo
{
    public void Bar()
    {
        int bad_var = 1;
        bad_var = 2;
        _ = bad_var;
    }
}";
        var vs = Analyze(src);
        var matches = vs.Where(v => v.Rule.Name == "CamelCaseVariableName"
            && v.Description.Contains("bad_var")).ToList();
        Assert.Single(matches);
    }

    // ─── combined fixture ─────────────────────────────────────────────────────

    [Fact]
    public void AllFiveRules_FireOnCombinedFixture()
    {
        var src = @"
public class bad_name
{
    private int bad_field;

    public void snake_method(int under_score)
    {
        int local_var = 1;
        _ = local_var;
    }
}";
        var vs = Analyze(src);
        MustHave(vs,
            "CamelCaseClassName",
            "CamelCaseMethodName",
            "CamelCasePropertyName",
            "CamelCaseParameterName",
            "CamelCaseVariableName");
    }

    [Fact]
    public void Idiomatic_CSharp_NoViolations()
    {
        // Idiomatic C# should be fully clean under the controversial ruleset
        var src = @"
public class MyService
{
    private readonly int _count;

    public MyService(int count)
    {
        _count = count;
    }

    public int GetCount() { return _count; }

    public int Compute(int value)
    {
        int result = value * _count;
        return result;
    }
}";
        var vs = Analyze(src);
        MustNotHave(vs,
            "CamelCaseClassName",
            "CamelCaseMethodName",
            "CamelCasePropertyName",
            "CamelCaseParameterName",
            "CamelCaseVariableName");
    }
}
