using MessCS.Model;
using MessCS.Rule;
using MessCS.Rules.CleanCode;
using Xunit;

namespace MessCS.Tests;

/// <summary>
/// Behavioral tests for the CleanCode ruleset.
/// Each test asserts which rules fire (or do not fire) on crafted C# fixtures.
/// Pattern mirrors messgo's rules_test.go: MustHave / MustNotHave helpers.
/// </summary>
public class CleanCodeRulesTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static List<Violation> Analyze(string source, BaseRule rule,
        Dictionary<string, string>? props = null)
    {
        var sf = ModelBuilder.Parse("test.cs", source);
        rule.RuleProps = new Properties(props);
        var violations = new List<Violation>();
        var ctx = new RuleContext(sf, rule, rule.RuleProps, violations);

        if (rule is IMethodRule mr)
        {
            foreach (var m in sf.AllMethods)
                mr.Apply(ctx, m);
            foreach (var iface in sf.Interfaces)
                foreach (var m in iface.Methods)
                    mr.Apply(ctx, m);
        }

        return violations;
    }

    private static void MustHave(List<Violation> violations, string ruleName)
    {
        Assert.True(violations.Count > 0,
            $"Expected rule '{ruleName}' to fire, but got no violations.");
    }

    private static void MustNotHave(List<Violation> violations, string ruleName)
    {
        Assert.Empty(violations);
    }

    // -------------------------------------------------------------------------
    // BooleanArgumentFlag
    // -------------------------------------------------------------------------

    [Fact]
    public void BooleanArgumentFlag_PublicMethodWithBoolParam_Fires()
    {
        var src = @"
public class Foo {
    public void Bar(bool flag) { }
}";
        var v = Analyze(src, MakeRule<BooleanArgumentFlagRule>("BooleanArgumentFlag"));
        MustHave(v, "BooleanArgumentFlag");
        Assert.Contains(v, x => x.Description.Contains("Bar") && x.Description.Contains("flag"));
    }

    [Fact]
    public void BooleanArgumentFlag_PrivateMethodWithBoolParam_DoesNotFire()
    {
        var src = @"
public class Foo {
    private void Bar(bool flag) { }
}";
        var v = Analyze(src, MakeRule<BooleanArgumentFlagRule>("BooleanArgumentFlag"));
        MustNotHave(v, "BooleanArgumentFlag");
    }

    [Fact]
    public void BooleanArgumentFlag_NonBoolParam_DoesNotFire()
    {
        var src = @"
public class Foo {
    public void Bar(int count) { }
}";
        var v = Analyze(src, MakeRule<BooleanArgumentFlagRule>("BooleanArgumentFlag"));
        MustNotHave(v, "BooleanArgumentFlag");
    }

    [Fact]
    public void BooleanArgumentFlag_ExceptionClassSkipped()
    {
        var src = @"
public class Foo {
    public void Bar(bool flag) { }
}";
        var v = Analyze(src, MakeRule<BooleanArgumentFlagRule>("BooleanArgumentFlag"),
            new Dictionary<string, string> { ["exceptions"] = "Foo" });
        MustNotHave(v, "BooleanArgumentFlag");
    }

    [Fact]
    public void BooleanArgumentFlag_IgnorePattern_Skips()
    {
        var src = @"
public class Foo {
    public void Bar(bool flag) { }
}";
        var v = Analyze(src, MakeRule<BooleanArgumentFlagRule>("BooleanArgumentFlag"),
            new Dictionary<string, string> { ["ignorepattern"] = "(^Bar)i" });
        MustNotHave(v, "BooleanArgumentFlag");
    }

    [Fact]
    public void BooleanArgumentFlag_ExactMessage()
    {
        var src = @"
public class Foo {
    public void Process(bool enabled) { }
}";
        var rule = MakeRule<BooleanArgumentFlagRule>("BooleanArgumentFlag");
        rule.Message = "The method {0} has a boolean flag argument {1}, which is a certain sign of a Single Responsibility Principle violation.";
        var v = Analyze(src, rule);
        Assert.Single(v);
        Assert.Equal(
            "The method Foo::Process has a boolean flag argument enabled, which is a certain sign of a Single Responsibility Principle violation.",
            v[0].Description);
    }

    [Fact]
    public void BooleanArgumentFlag_NullableBool_Fires()
    {
        var src = @"
public class Foo {
    public void Bar(bool? flag) { }
}";
        var v = Analyze(src, MakeRule<BooleanArgumentFlagRule>("BooleanArgumentFlag"));
        MustHave(v, "BooleanArgumentFlag");
    }

    // -------------------------------------------------------------------------
    // ElseExpression
    // -------------------------------------------------------------------------

    [Fact]
    public void ElseExpression_PlainElse_Fires()
    {
        var src = @"
public class Foo {
    public void Bar(bool flag) {
        if (flag) {
            // one branch
        } else {
            // another branch
        }
    }
}";
        var v = Analyze(src, MakeRule<ElseExpressionRule>("ElseExpression"));
        MustHave(v, "ElseExpression");
    }

    [Fact]
    public void ElseExpression_ElseIf_DoesNotFire()
    {
        var src = @"
public class Foo {
    public void Bar(int x) {
        if (x == 1) {
            // branch 1
        } else if (x == 2) {
            // branch 2
        } else if (x == 3) {
            // branch 3
        }
    }
}";
        var v = Analyze(src, MakeRule<ElseExpressionRule>("ElseExpression"));
        MustNotHave(v, "ElseExpression");
    }

    [Fact]
    public void ElseExpression_ElseIfWithTrailingElse_FiresOnce()
    {
        // The trailing plain else fires; the else-if chain does not
        var src = @"
public class Foo {
    public void Bar(int x) {
        if (x == 1) {
            // branch 1
        } else if (x == 2) {
            // branch 2
        } else {
            // final else fires
        }
    }
}";
        var v = Analyze(src, MakeRule<ElseExpressionRule>("ElseExpression"));
        Assert.Single(v);
    }

    [Fact]
    public void ElseExpression_NoElse_DoesNotFire()
    {
        var src = @"
public class Foo {
    public void Bar(bool flag) {
        if (flag) {
            return;
        }
        DoOther();
    }
    private void DoOther() { }
}";
        var v = Analyze(src, MakeRule<ElseExpressionRule>("ElseExpression"));
        MustNotHave(v, "ElseExpression");
    }

    [Fact]
    public void ElseExpression_ExactMessage()
    {
        var src = @"
public class Foo {
    public void Compute(bool flag) {
        if (flag) {
            return;
        } else {
            return;
        }
    }
}";
        var rule = MakeRule<ElseExpressionRule>("ElseExpression");
        rule.Message = "The method {0} uses an else expression. Else clauses are basically not necessary and you can simplify the code by not using them.";
        var v = Analyze(src, rule);
        Assert.Single(v);
        Assert.Equal(
            "The method Compute uses an else expression. Else clauses are basically not necessary and you can simplify the code by not using them.",
            v[0].Description);
    }

    // -------------------------------------------------------------------------
    // IfStatementAssignment
    // -------------------------------------------------------------------------

    [Fact]
    public void IfStatementAssignment_AssignInIfCondition_Fires()
    {
        // C# requires extra parens for assignment in condition
        var src = @"
public class Foo {
    public void Bar(string input) {
        string result = null;
        if ((result = Process(input)) != null) {
            Use(result);
        }
    }
    private string Process(string s) => s;
    private void Use(string s) { }
}";
        var v = Analyze(src, MakeRule<IfStatementAssignmentRule>("IfStatementAssignment"));
        MustHave(v, "IfStatementAssignment");
    }

    [Fact]
    public void IfStatementAssignment_AssignInWhileCondition_Fires()
    {
        var src = @"
public class Foo {
    public void Bar() {
        string line;
        while ((line = ReadLine()) != null) {
            Process(line);
        }
    }
    private string ReadLine() => null;
    private void Process(string s) { }
}";
        var v = Analyze(src, MakeRule<IfStatementAssignmentRule>("IfStatementAssignment"));
        MustHave(v, "IfStatementAssignment");
    }

    [Fact]
    public void IfStatementAssignment_NoAssignment_DoesNotFire()
    {
        var src = @"
public class Foo {
    public void Bar(int x) {
        if (x > 0) {
            return;
        }
    }
}";
        var v = Analyze(src, MakeRule<IfStatementAssignmentRule>("IfStatementAssignment"));
        MustNotHave(v, "IfStatementAssignment");
    }

    [Fact]
    public void IfStatementAssignment_ComparisonEquality_DoesNotFire()
    {
        var src = @"
public class Foo {
    public void Bar(int x) {
        if (x == 5) {
            return;
        }
    }
}";
        var v = Analyze(src, MakeRule<IfStatementAssignmentRule>("IfStatementAssignment"));
        MustNotHave(v, "IfStatementAssignment");
    }

    // -------------------------------------------------------------------------
    // DuplicatedArrayKey
    // -------------------------------------------------------------------------

    [Fact]
    public void DuplicatedArrayKey_IndexerSyntax_Fires()
    {
        var src = @"
using System.Collections.Generic;
public class Foo {
    public void Bar() {
        var d = new Dictionary<string, int> {
            [""foo""] = 1,
            [""foo""] = 2,
        };
    }
}";
        var v = Analyze(src, MakeRule<DuplicatedArrayKeyRule>("DuplicatedArrayKey"));
        MustHave(v, "DuplicatedArrayKey");
    }

    [Fact]
    public void DuplicatedArrayKey_ComplexInitializer_Fires()
    {
        var src = @"
using System.Collections.Generic;
public class Foo {
    public void Bar() {
        var d = new Dictionary<string, int> {
            { ""foo"", 1 },
            { ""foo"", 2 },
        };
    }
}";
        var v = Analyze(src, MakeRule<DuplicatedArrayKeyRule>("DuplicatedArrayKey"));
        MustHave(v, "DuplicatedArrayKey");
    }

    [Fact]
    public void DuplicatedArrayKey_UniqueKeys_DoesNotFire()
    {
        var src = @"
using System.Collections.Generic;
public class Foo {
    public void Bar() {
        var d = new Dictionary<string, int> {
            [""foo""] = 1,
            [""bar""] = 2,
            [""baz""] = 3,
        };
    }
}";
        var v = Analyze(src, MakeRule<DuplicatedArrayKeyRule>("DuplicatedArrayKey"));
        MustNotHave(v, "DuplicatedArrayKey");
    }

    [Fact]
    public void DuplicatedArrayKey_DuplicateMessage_ContainsKey()
    {
        var src = @"
using System.Collections.Generic;
public class Foo {
    public void Bar() {
        var d = new Dictionary<string, int> {
            [""alpha""] = 10,
            [""alpha""] = 20,
        };
    }
}";
        var rule = MakeRule<DuplicatedArrayKeyRule>("DuplicatedArrayKey");
        rule.Message = "Duplicated array key {0}, first declared at line {1}.";
        var v = Analyze(src, rule);
        Assert.Single(v);
        Assert.Contains("\"alpha\"", v[0].Description);
    }

    // -------------------------------------------------------------------------
    // StaticAccess
    // -------------------------------------------------------------------------

    [Fact]
    public void StaticAccess_OtherClassStaticCall_Fires()
    {
        var src = @"
public class Foo {
    public void Bar() {
        Baz.DoSomething();
    }
}
public class Baz {
    public static void DoSomething() { }
}";
        var v = Analyze(src, MakeRule<StaticAccessRule>("StaticAccess"));
        MustHave(v, "StaticAccess");
    }

    [Fact]
    public void StaticAccess_ExceptionClass_DoesNotFire()
    {
        var src = @"
public class Foo {
    public void Bar() {
        Baz.DoSomething();
    }
}
public class Baz {
    public static void DoSomething() { }
}";
        var v = Analyze(src, MakeRule<StaticAccessRule>("StaticAccess"),
            new Dictionary<string, string> { ["exceptions"] = "Baz" });
        MustNotHave(v, "StaticAccess");
    }

    [Fact]
    public void StaticAccess_OwnClass_DoesNotFire()
    {
        var src = @"
public class Foo {
    public void Bar() {
        Foo.Helper();
    }
    public static void Helper() { }
}";
        var v = Analyze(src, MakeRule<StaticAccessRule>("StaticAccess"));
        MustNotHave(v, "StaticAccess");
    }

    [Fact]
    public void StaticAccess_IgnorePattern_Skips()
    {
        var src = @"
public class Foo {
    public void Create() {
        Builder.Build();
    }
}
public class Builder {
    public static void Build() { }
}";
        var v = Analyze(src, MakeRule<StaticAccessRule>("StaticAccess"),
            new Dictionary<string, string> { ["ignorepattern"] = "(^create)i" });
        MustNotHave(v, "StaticAccess");
    }

    [Fact]
    public void StaticAccess_InstanceMethodCall_DoesNotFire()
    {
        var src = @"
public class Foo {
    public void Bar(Baz b) {
        b.DoSomething();
    }
}
public class Baz {
    public void DoSomething() { }
}";
        var v = Analyze(src, MakeRule<StaticAccessRule>("StaticAccess"));
        MustNotHave(v, "StaticAccess");
    }

    [Fact]
    public void StaticAccess_ExactMessage()
    {
        var src = @"
public class Foo {
    public void Run() {
        Logger.Log(""msg"");
    }
}
public class Logger {
    public static void Log(string msg) { }
}";
        var rule = MakeRule<StaticAccessRule>("StaticAccess");
        rule.Message = "Avoid using static access to class '{0}' in method '{1}'.";
        var v = Analyze(src, rule);
        Assert.Single(v);
        Assert.Equal("Avoid using static access to class 'Logger' in method 'Run'.", v[0].Description);
    }

    [Fact]
    public void StaticAccess_MultipleExceptions_AllSkipped()
    {
        var src = @"
public class Foo {
    public void Bar() {
        Math.Abs(-1);
        Convert.ToString(42);
        Baz.Call();
    }
}
public class Baz {
    public static void Call() { }
}";
        var v = Analyze(src, MakeRule<StaticAccessRule>("StaticAccess"),
            new Dictionary<string, string> { ["exceptions"] = "Math,Convert" });
        // Only Baz.Call fires
        Assert.Single(v);
        Assert.Contains("Baz", v[0].Description);
    }

    // -------------------------------------------------------------------------
    // Factory registration smoke test
    // -------------------------------------------------------------------------

    [Fact]
    public void Factories_AllFiveRulesRegistered()
    {
        var keys = CleanCodeRules.Factories.Keys.ToList();
        Assert.Contains("PHPMD\\Rule\\CleanCode\\BooleanArgumentFlag", keys);
        Assert.Contains("PHPMD\\Rule\\CleanCode\\ElseExpression", keys);
        Assert.Contains("PHPMD\\Rule\\CleanCode\\IfStatementAssignment", keys);
        Assert.Contains("PHPMD\\Rule\\CleanCode\\DuplicatedArrayKey", keys);
        Assert.Contains("PHPMD\\Rule\\CleanCode\\StaticAccess", keys);
    }

    [Fact]
    public void All_ReturnsFiveRules()
    {
        Assert.Equal(5, CleanCodeRules.All.Count);
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static T MakeRule<T>(string name) where T : BaseRule, new() =>
        new T { Name = name, Message = "{0} {1}", Priority = 1, SetName = "cleancode" };
}
