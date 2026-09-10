using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.Rules.Naming;
using Xunit;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.Tests;

/// <summary>
/// Behavioral tests for the Naming rule group.
/// Ports messgo's naming test cases and adds C#-specific coverage.
/// Assertions mirror the MustHave/MustNotHave pattern from messgo's rules_test.go.
/// </summary>
public class NamingRulesTests
{
    // ------------------------------------------------------------------ helpers

    private static RuleSetType MakeSet(params BaseRule[] rules) =>
        new() { Name = "naming", Rules = rules.Cast<IRule>().ToList() };

    private static List<Violation> Run(string source, params BaseRule[] rules)
    {
        var sf = ModelBuilder.Parse("test.cs", source);
        return Engine.Analyze(sf, new[] { MakeSet(rules) });
    }

    private static BaseRule MakeRule<T>(string name, string message,
        Dictionary<string, string>? props = null) where T : BaseRule, new() =>
        new T
        {
            Name = name,
            Message = message,
            Priority = 3,
            SetName = "naming",
            ExternalUrl = "",
            Description = "",
            Since = "0.1",
            RuleProps = new Properties(props),
        };

    private static void MustHave(List<Violation> violations, string ruleName) =>
        Assert.True(violations.Any(v => v.Rule.Name == ruleName),
            $"Expected violation '{ruleName}' but none found. Violations: [{string.Join(", ", violations.Select(v => v.Rule.Name))}]");

    private static void MustNotHave(List<Violation> violations, string ruleName) =>
        Assert.False(violations.Any(v => v.Rule.Name == ruleName),
            $"Expected no violation '{ruleName}' but one was found.");

    // ------------------------------------------------------------------ ShortClassName

    [Fact]
    public void ShortClassName_ShortClass_ReportsViolation()
    {
        var src = @"
class Fo
{
}";
        var rule = MakeRule<ShortClassNameRule>("ShortClassName",
            "Avoid classes with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "ShortClassName");
    }

    [Fact]
    public void ShortClassName_SecondNamespace_PackageIsDeclaringNamespace()
    {
        var src = @"
namespace First { class FirstType { } }
namespace Second { class Ab { } }";
        var rule = MakeRule<ShortClassNameRule>("ShortClassName",
            "Avoid classes with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        var v = Assert.Single(violations);
        Assert.Equal("Second", v.Package);
        Assert.Contains("Ab", v.Description);
    }

    [Fact]
    public void ShortClassName_LongEnoughClass_NoViolation()
    {
        var src = @"
class Foo
{
}";
        var rule = MakeRule<ShortClassNameRule>("ShortClassName",
            "Avoid classes with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        MustNotHave(violations, "ShortClassName");
    }

    [Fact]
    public void ShortClassName_ShortInterface_ReportsViolation()
    {
        var src = @"
interface Fo
{
}";
        var rule = MakeRule<ShortClassNameRule>("ShortClassName",
            "Avoid classes with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "ShortClassName");
    }

    [Fact]
    public void ShortClassName_Exception_NoViolation()
    {
        var src = @"
class Fo
{
}";
        var rule = MakeRule<ShortClassNameRule>("ShortClassName",
            "Avoid classes with short names like {0}. Configured minimum length is {1}.",
            new() { ["exceptions"] = "Fo" });
        var violations = Run(src, rule);
        MustNotHave(violations, "ShortClassName");
    }

    [Fact]
    public void ShortClassName_Message_ContainsNameAndMinimum()
    {
        var src = @"
class Fo
{
}";
        var rule = MakeRule<ShortClassNameRule>("ShortClassName",
            "Avoid classes with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        Assert.Single(violations);
        Assert.Contains("Fo", violations[0].Description);
        Assert.Contains("3", violations[0].Description);
    }

    // ------------------------------------------------------------------ LongClassName

    [Fact]
    public void LongClassName_TooLongClass_ReportsViolation()
    {
        var src = @"
class ATooLongClassNameThatHintsAtADesignProblem
{
}";
        var rule = MakeRule<LongClassNameRule>("LongClassName",
            "Avoid excessively long class names like {0}. Keep class name length under {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "LongClassName");
    }

    [Fact]
    public void LongClassName_ShortEnoughClass_NoViolation()
    {
        var src = @"
class ShortEnoughName
{
}";
        var rule = MakeRule<LongClassNameRule>("LongClassName",
            "Avoid excessively long class names like {0}. Keep class name length under {1}.");
        var violations = Run(src, rule);
        MustNotHave(violations, "LongClassName");
    }

    [Fact]
    public void LongClassName_SubtractSuffix_DoesNotViolate()
    {
        // "AbstractSomeLongishName" = 23 chars, subtract "Abstract" prefix => "SomeLongishName" = 15 <= 20
        var src = @"
class AbstractSomeLongishName
{
}";
        var rule = MakeRule<LongClassNameRule>("LongClassName",
            "Avoid excessively long class names like {0}. Keep class name length under {1}.",
            new() { ["maximum"] = "20", ["subtract-prefixes"] = "Abstract" });
        var violations = Run(src, rule);
        MustNotHave(violations, "LongClassName");
    }

    // ------------------------------------------------------------------ ShortVariable

    [Fact]
    public void ShortVariable_ShortField_ReportsViolation()
    {
        var src = @"
class Foo
{
    private int _q = 15;
}";
        var rule = MakeRule<ShortVariableRule>("ShortVariable",
            "Avoid variables with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "ShortVariable");
    }

    [Fact]
    public void ShortVariable_ShortParam_ReportsViolation()
    {
        var src = @"
class Foo
{
    public void Bar(int xs) { }
}";
        var rule = MakeRule<ShortVariableRule>("ShortVariable",
            "Avoid variables with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "ShortVariable");
    }

    [Fact]
    public void ShortVariable_ShortLocal_TwoChars_ReportsViolation()
    {
        var src = @"
class Foo
{
    public void Bar()
    {
        int bx = 5;
    }
}";
        var rule = MakeRule<ShortVariableRule>("ShortVariable",
            "Avoid variables with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        // "bx" is length 2, minimum is 3 → should fire
        MustHave(violations, "ShortVariable");
    }

    [Fact]
    public void ShortVariable_DeclarationPattern_ReportsViolation()
    {
        var src = @"
class C
{
    public void M()
    {
        if (new object() is int x) { }
    }
}";
        var rule = MakeRule<ShortVariableRule>("ShortVariable",
            "Avoid variables with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "ShortVariable");
        Assert.Contains(violations, v => v.Rule.Name == "ShortVariable"
            && v.Description.Contains("x"));
    }

    [Fact]
    public void ShortVariable_DeclarationPatternDiscard_NoViolation()
    {
        var src = @"
class C
{
    public void M()
    {
        if (new object() is int _) { }
    }
}";
        var rule = MakeRule<ShortVariableRule>("ShortVariable",
            "Avoid variables with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        MustNotHave(violations, "ShortVariable");
    }

    [Fact]
    public void ShortVariable_ForLoopCounter_NoViolation()
    {
        var src = @"
class Foo
{
    public void Bar()
    {
        for (int i = 0; i < 10; i++) { }
    }
}";
        var rule = MakeRule<ShortVariableRule>("ShortVariable",
            "Avoid variables with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        MustNotHave(violations, "ShortVariable");
    }

    [Fact]
    public void ShortVariable_Exception_NoViolation()
    {
        var src = @"
class Foo
{
    private int _q = 15;
}";
        var rule = MakeRule<ShortVariableRule>("ShortVariable",
            "Avoid variables with short names like {0}. Configured minimum length is {1}.",
            new() { ["exceptions"] = "_q" });
        var violations = Run(src, rule);
        MustNotHave(violations, "ShortVariable");
    }

    // ------------------------------------------------------------------ LongVariable

    [Fact]
    public void LongVariable_LongField_ReportsViolation()
    {
        var src = @"
class Foo
{
    private int _reallyLongIntNameVariable = -3;
}";
        var rule = MakeRule<LongVariableRule>("LongVariable",
            "Avoid excessively long variable names like {0}. Keep variable name length under {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "LongVariable");
    }

    [Fact]
    public void LongVariable_ShortField_NoViolation()
    {
        var src = @"
class Foo
{
    private int _count = 0;
}";
        var rule = MakeRule<LongVariableRule>("LongVariable",
            "Avoid excessively long variable names like {0}. Keep variable name length under {1}.");
        var violations = Run(src, rule);
        MustNotHave(violations, "LongVariable");
    }

    [Fact]
    public void LongVariable_LongParam_ReportsViolation()
    {
        var src = @"
class Foo
{
    public void Method(int interestingArgumentsList) { }
}";
        var rule = MakeRule<LongVariableRule>("LongVariable",
            "Avoid excessively long variable names like {0}. Keep variable name length under {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "LongVariable");
    }

    [Fact]
    public void LongVariable_SubtractPrefix_DoesNotViolate()
    {
        // "_reallyLongName" = 15 chars, subtract "_" prefix => "reallyLongName" = 14 <= 20
        var src = @"
class Foo
{
    private int _reallyLongName = 0;
}";
        var rule = MakeRule<LongVariableRule>("LongVariable",
            "Avoid excessively long variable names like {0}. Keep variable name length under {1}.",
            new() { ["maximum"] = "20", ["subtract-prefixes"] = "_" });
        var violations = Run(src, rule);
        MustNotHave(violations, "LongVariable");
    }

    // ------------------------------------------------------------------ ShortMethodName

    [Fact]
    public void ShortMethodName_ShortMethod_ReportsViolation()
    {
        var src = @"
class Foo
{
    public void A(int index) { }
}";
        var rule = MakeRule<ShortMethodNameRule>("ShortMethodName",
            "Avoid using short method names like {0}::{1}(). The configured minimum method name length is {2}.");
        var violations = Run(src, rule);
        MustHave(violations, "ShortMethodName");
    }

    [Fact]
    public void ShortMethodName_LongEnoughMethod_NoViolation()
    {
        var src = @"
class Foo
{
    public void Run() { }
}";
        var rule = MakeRule<ShortMethodNameRule>("ShortMethodName",
            "Avoid using short method names like {0}::{1}(). The configured minimum method name length is {2}.");
        var violations = Run(src, rule);
        MustNotHave(violations, "ShortMethodName");
    }

    [Fact]
    public void ShortMethodName_Exception_NoViolation()
    {
        var src = @"
class Foo
{
    public void Go() { }
}";
        var rule = MakeRule<ShortMethodNameRule>("ShortMethodName",
            "Avoid using short method names like {0}::{1}(). The configured minimum method name length is {2}.",
            new() { ["exceptions"] = "Go" });
        var violations = Run(src, rule);
        MustNotHave(violations, "ShortMethodName");
    }

    [Fact]
    public void ShortMethodName_Constructor_WhenClassNameIsShort_NoViolation()
    {
        var src = @"
public class Db
{
    public Db() { }
    public Db(int x) { }
}";
        var rule = MakeRule<ShortMethodNameRule>("ShortMethodName",
            "Avoid using short method names like {0}::{1}(). The configured minimum method name length is {2}.");
        var violations = Run(src, rule);
        MustNotHave(violations, "ShortMethodName");
    }

    [Fact]
    public void ShortMethodName_ShortMethodInShortClass_StillReportsViolation()
    {
        var src = @"
public class Db
{
    public Db() { }
    public void A() { }
}";
        var rule = MakeRule<ShortMethodNameRule>("ShortMethodName",
            "Avoid using short method names like {0}::{1}(). The configured minimum method name length is {2}.");
        var violations = Run(src, rule);
        MustHave(violations, "ShortMethodName");
        Assert.Single(violations);
        Assert.Contains("Db::A()", violations[0].Description);
    }

    [Fact]
    public void ShortMethodName_Message_ContainsClassAndMethod()
    {
        var src = @"
class MyClass
{
    public void A() { }
}";
        var rule = MakeRule<ShortMethodNameRule>("ShortMethodName",
            "Avoid using short method names like {0}::{1}(). The configured minimum method name length is {2}.");
        var violations = Run(src, rule);
        Assert.Single(violations);
        Assert.Equal("MyClass", violations[0].Class);
        Assert.Contains("MyClass", violations[0].Description);
        Assert.Contains("A", violations[0].Description);
        Assert.Contains("3", violations[0].Description);
    }

    [Fact]
    public void ShortMethodName_InterfaceMethod_ContainsInterfaceAndMethod()
    {
        var src = @"
public interface IFoo
{
    void A();
}";
        var rule = MakeRule<ShortMethodNameRule>("ShortMethodName",
            "Avoid using short method names like {0}::{1}(). The configured minimum method name length is {2}.");
        var violations = Run(src, rule);
        Assert.Single(violations);
        Assert.Equal("IFoo", violations[0].Class);
        Assert.Contains("IFoo::A()", violations[0].Description);
    }

    // ------------------------------------------------------------------ ConstantNamingConventions

    [Fact]
    public void ConstantNaming_LowerCaseConst_Pascal_ReportsViolation()
    {
        var src = @"
class Foo
{
    const string myTest = """";
}";
        var rule = MakeRule<ConstantNamingConventionsRule>("ConstantNamingConventions",
            "Constant {0} should be defined in PascalCase");
        var violations = Run(src, rule);
        MustHave(violations, "ConstantNamingConventions");
        Assert.Contains("myTest", violations[0].Description);
    }

    [Fact]
    public void ConstantNaming_PascalCaseConst_Pascal_NoViolation()
    {
        var src = @"
class Foo
{
    const int MyNum = 0;
}";
        var rule = MakeRule<ConstantNamingConventionsRule>("ConstantNamingConventions",
            "Constant {0} should be defined in PascalCase");
        var violations = Run(src, rule);
        MustNotHave(violations, "ConstantNamingConventions");
    }

    [Fact]
    public void ConstantNaming_AllCapsConst_Upper_NoViolation()
    {
        var src = @"
class Foo
{
    const int MY_NUM = 0;
}";
        var rule = MakeRule<ConstantNamingConventionsRule>("ConstantNamingConventions",
            "Constant {0} should be defined in uppercase",
            new() { ["convention"] = "upper" });
        var violations = Run(src, rule);
        MustNotHave(violations, "ConstantNamingConventions");
    }

    [Fact]
    public void ConstantNaming_DigitsInUpper_NoViolation()
    {
        var src = @"
class Foo
{
    const int HTTP_200_OK = 200;
    const string SHA256 = ""sha"";
}";
        var rule = MakeRule<ConstantNamingConventionsRule>("ConstantNamingConventions",
            "Constant {0} should be defined in uppercase",
            new() { ["convention"] = "upper" });
        var violations = Run(src, rule);
        MustNotHave(violations, "ConstantNamingConventions");
    }

    [Fact]
    public void ConstantNaming_PascalCaseConst_Upper_ReportsViolation()
    {
        var src = @"
class Foo
{
    const int MyNum = 0;
}";
        var rule = MakeRule<ConstantNamingConventionsRule>("ConstantNamingConventions",
            "Constant {0} should be defined in uppercase",
            new() { ["convention"] = "upper" });
        var violations = Run(src, rule);
        MustHave(violations, "ConstantNamingConventions");
    }

    [Fact]
    public void ConstantNaming_UnderscoreInPascal_ReportsViolation()
    {
        var src = @"
class Foo
{
    const int MY_NUM = 0;
}";
        var rule = MakeRule<ConstantNamingConventionsRule>("ConstantNamingConventions",
            "Constant {0} should be defined in PascalCase");
        var violations = Run(src, rule);
        MustHave(violations, "ConstantNamingConventions");
    }

    // ------------------------------------------------------------------ BooleanGetMethodName

    [Fact]
    public void BooleanGetMethodName_GetBoolMethod_ReportsViolation()
    {
        var src = @"
class Foo
{
    public bool GetFoo() { return true; }
}";
        var rule = MakeRule<BooleanGetMethodNameRule>("BooleanGetMethodName",
            "The '{0}()' method which returns a boolean should be named 'Is...()' or 'Has...()'");
        var violations = Run(src, rule);
        MustHave(violations, "BooleanGetMethodName");
        Assert.Contains("GetFoo", violations[0].Description);
    }

    [Fact]
    public void BooleanGetMethodName_FullyQualifiedSystemBoolean_ReportsViolation()
    {
        var src = @"
class Foo
{
    public System.Boolean GetFoo() { return true; }
}";
        var rule = MakeRule<BooleanGetMethodNameRule>("BooleanGetMethodName",
            "The '{0}()' method which returns a boolean should be named 'Is...()' or 'Has...()'");
        var violations = Run(src, rule);
        MustHave(violations, "BooleanGetMethodName");
    }

    [Fact]
    public void BooleanGetMethodName_FullyQualifiedNonBoolean_NoViolation()
    {
        var src = @"
class Foo
{
    public System.Int32 GetFoo() { return 0; }
}";
        var rule = MakeRule<BooleanGetMethodNameRule>("BooleanGetMethodName",
            "The '{0}()' method which returns a boolean should be named 'Is...()' or 'Has...()'");
        var violations = Run(src, rule);
        MustNotHave(violations, "BooleanGetMethodName");
    }

    [Fact]
    public void BooleanGetMethodName_IsBoolMethod_NoViolation()
    {
        var src = @"
class Foo
{
    public bool IsFoo() { return true; }
}";
        var rule = MakeRule<BooleanGetMethodNameRule>("BooleanGetMethodName",
            "The '{0}()' method which returns a boolean should be named 'Is...()' or 'Has...()'");
        var violations = Run(src, rule);
        MustNotHave(violations, "BooleanGetMethodName");
    }

    [Fact]
    public void BooleanGetMethodName_GetWithParams_Default_NoViolation()
    {
        // Default: checkParameterizedMethods=false, so methods with params are exempt
        var src = @"
class Foo
{
    public bool GetFoo(int bar) { return true; }
}";
        var rule = MakeRule<BooleanGetMethodNameRule>("BooleanGetMethodName",
            "The '{0}()' method which returns a boolean should be named 'Is...()' or 'Has...()'");
        var violations = Run(src, rule);
        MustNotHave(violations, "BooleanGetMethodName");
    }

    [Fact]
    public void BooleanGetMethodName_GetWithParams_CheckEnabled_ReportsViolation()
    {
        var src = @"
class Foo
{
    public bool GetFoo(int bar) { return true; }
}";
        var rule = MakeRule<BooleanGetMethodNameRule>("BooleanGetMethodName",
            "The '{0}()' method which returns a boolean should be named 'Is...()' or 'Has...()'",
            new() { ["checkParameterizedMethods"] = "true" });
        var violations = Run(src, rule);
        MustHave(violations, "BooleanGetMethodName");
    }

    [Fact]
    public void BooleanGetMethodName_NonBoolReturn_NoViolation()
    {
        var src = @"
class Foo
{
    public int GetFoo() { return 0; }
}";
        var rule = MakeRule<BooleanGetMethodNameRule>("BooleanGetMethodName",
            "The '{0}()' method which returns a boolean should be named 'Is...()' or 'Has...()'");
        var violations = Run(src, rule);
        MustNotHave(violations, "BooleanGetMethodName");
    }

    // ------------------------------------------------------------------ Combined (ports messgo TestNaming)

    [Fact]
    public void AllNamingRules_MessgoTestNamingEquivalent_AllFire()
    {
        // Port of messgo's TestNaming: exercises ShortClassName, ShortVariable,
        // ShortMethodName, BooleanGetMethodName, ConstantNamingConventions
        var src = @"
class Fo
{
    private int _x = 0;

    public int A(int b) { return b; }
}

class GetActiveTester
{
    public bool GetActive() { return true; }
}";

        var shortClass = MakeRule<ShortClassNameRule>("ShortClassName",
            "Avoid classes with short names like {0}. Configured minimum length is {1}.");
        var shortVar = MakeRule<ShortVariableRule>("ShortVariable",
            "Avoid variables with short names like {0}. Configured minimum length is {1}.");
        var shortMethod = MakeRule<ShortMethodNameRule>("ShortMethodName",
            "Avoid using short method names like {0}::{1}(). The configured minimum method name length is {2}.");
        var boolGet = MakeRule<BooleanGetMethodNameRule>("BooleanGetMethodName",
            "The '{0}()' method which returns a boolean should be named 'Is...()' or 'Has...()'");

        var violations = Run(src, shortClass, shortVar, shortMethod, boolGet);

        MustHave(violations, "ShortClassName");
        MustHave(violations, "ShortVariable");
        MustHave(violations, "ShortMethodName");
        MustHave(violations, "BooleanGetMethodName");
    }

    [Fact]
    public void ShortVariable_ForEachLoopVar_IsReported()
    {
        var src = @"
class Foo
{
    public void Bar()
    {
        var items = new[] { 1, 2, 3 };
        foreach (var it in items) { }
    }
}";
        var rule = MakeRule<ShortVariableRule>("ShortVariable",
            "Avoid variables with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "ShortVariable");
        Assert.Contains(violations, v => v.Rule.Name == "ShortVariable"
            && v.Description.Contains("it"));
    }

    [Fact]
    public void LongVariable_ForEachLoopVar_IsReported()
    {
        var src = @"
class Foo
{
    public void Bar()
    {
        var items = new[] { 1, 2, 3 };
        foreach (var excessivelyLongForeachVariableName in items) { }
    }
}";
        var rule = MakeRule<LongVariableRule>("LongVariable",
            "Avoid excessively long variable names like {0}. Keep variable name length under {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "LongVariable");
        Assert.Contains(violations, v => v.Rule.Name == "LongVariable"
            && v.Description.Contains("excessivelyLongForeachVariableName"));
    }

    [Fact]
    public void ShortVariable_DeconstructedForEachVars_AreReported()
    {
        var src = @"
class Foo
{
    public void Bar()
    {
        var items = new[] { (1, 2) };
        foreach (var (x, value) in items) { }
    }
}";
        var rule = MakeRule<ShortVariableRule>("ShortVariable",
            "Avoid variables with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "ShortVariable");
        Assert.Contains(violations, v => v.Rule.Name == "ShortVariable"
            && v.Description.Contains("x"));
    }

    [Fact]
    public void ShortVariable_RegularLocal_Short_IsReported()
    {
        var src = @"
class Foo
{
    public void Bar()
    {
        int ab = 5;
    }
}";
        var rule = MakeRule<ShortVariableRule>("ShortVariable",
            "Avoid variables with short names like {0}. Configured minimum length is {1}.");
        var violations = Run(src, rule);
        MustHave(violations, "ShortVariable");
    }
}
