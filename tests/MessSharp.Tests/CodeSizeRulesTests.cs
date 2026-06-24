using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.Rules.CodeSize;
using Xunit;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.Tests;

/// <summary>
/// Behavioral tests for the codesize rule group. Each test exercises crafted
/// C# fixture source, asserting which rules fire and which don't.
/// Mirrors the structure of messgo's rules_test.go TestCodeSize cases.
/// </summary>
public class CodeSizeRulesTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static List<Violation> Analyze(string source, params (string key, string val)[] overrides)
    {
        var sf = ModelBuilder.Parse("fixture.cs", source);
        var set = BuildFullCodeSizeSet(overrides);
        return Engine.Analyze(sf, new[] { set });
    }

    private static bool Has(List<Violation> vs, string ruleName) =>
        vs.Any(v => v.Rule.Name == ruleName);

    private static void MustHave(List<Violation> vs, params string[] names)
    {
        foreach (var n in names)
            Assert.True(Has(vs, n), $"Expected rule '{n}' to fire; got: [{string.Join(", ", vs.Select(v => v.Rule.Name))}]");
    }

    private static void MustNotHave(List<Violation> vs, params string[] names)
    {
        foreach (var n in names)
            Assert.False(Has(vs, n), $"Did not expect rule '{n}' to fire; got: [{string.Join(", ", vs.Select(v => v.Rule.Name))}]");
    }

    private static RuleSetType BuildFullCodeSizeSet(
        (string key, string val)[] overrides)
    {
        var props = new Dictionary<string, string>();
        foreach (var (k, v) in overrides)
            props[k] = v;
        var propObj = new Properties(props);

        var rules = new List<IRule>();
        foreach (var factory in CodeSizeRules.Factories.Values)
        {
            var rule = factory();
            rule.Name = RuleNameFor(rule);
            rule.Message = MessageFor(rule);
            rule.Priority = 3;
            rule.SetName = "codesize";
            rule.RuleProps = propObj;
            rules.Add(rule);
        }
        return new RuleSetType { Name = "codesize", Rules = rules };
    }

    private static RuleSetType BuildSingleRule<T>(Dictionary<string, string>? props = null)
        where T : BaseRule, new()
    {
        var rule = new T
        {
            Name = RuleNameFor(new T()),
            Message = MessageFor(new T()),
            Priority = 3,
            SetName = "codesize",
            RuleProps = new Properties(props),
        };
        return new RuleSetType { Name = "codesize", Rules = { rule } };
    }

    private static string RuleNameFor(BaseRule r) => r switch
    {
        CyclomaticComplexityRule => "CyclomaticComplexity",
        NPathComplexityRule => "NPathComplexity",
        ExcessiveMethodLengthRule => "ExcessiveMethodLength",
        ExcessiveClassLengthRule => "ExcessiveClassLength",
        ExcessiveParameterListRule => "ExcessiveParameterList",
        ExcessivePublicCountRule => "ExcessivePublicCount",
        TooManyFieldsRule => "TooManyFields",
        TooManyMethodsRule => "TooManyMethods",
        TooManyPublicMethodsRule => "TooManyPublicMethods",
        ExcessiveClassComplexityRule => "ExcessiveClassComplexity",
        _ => r.GetType().Name,
    };

    private static string MessageFor(BaseRule r) => r switch
    {
        CyclomaticComplexityRule =>
            "The {0} {1}() has a Cyclomatic Complexity of {2}. The configured cyclomatic complexity threshold is {3}.",
        NPathComplexityRule =>
            "The {0} {1}() has an NPath complexity of {2}. The configured NPath complexity threshold is {3}.",
        ExcessiveMethodLengthRule =>
            "The {0} {1}() has {2} lines of code. Current threshold is set to {3}. Avoid really long methods.",
        ExcessiveClassLengthRule =>
            "The class {0} has {1} lines of code. Current threshold is {2}. Avoid really long classes.",
        ExcessiveParameterListRule =>
            "The {0} {1} has {2} parameters. Consider reducing the number of parameters to less than {3}.",
        ExcessivePublicCountRule =>
            "The {0} {1} has {2} public methods and attributes. Consider reducing the number of public items to less than {3}.",
        TooManyFieldsRule =>
            "The {0} {1} has {2} fields. Consider redesigning {1} to keep the number of fields under {3}.",
        TooManyMethodsRule =>
            "The {0} {1} has {2} non-getter- and setter-methods. Consider refactoring {1} to keep number of methods under {3}.",
        TooManyPublicMethodsRule =>
            "The {0} {1} has {2} public methods. Consider refactoring {1} to keep number of public methods under {3}.",
        ExcessiveClassComplexityRule =>
            "The class {0} has an overall complexity of {1} which is very high. The configured complexity threshold is {2}.",
        _ => "",
    };

    // -----------------------------------------------------------------------
    // Port of messgo TestCodeSize: manyParams + Big struct
    // -----------------------------------------------------------------------

    [Fact]
    public void ExcessiveParameterList_FiresOnManyParams()
    {
        // 11 parameters exceeds default threshold of 10
        var src = @"
public class Fixture {
    public void ManyParams(int a, int b, int c, int d, int e,
        int f, int g, int h, int i, int j, int k) {}
}";
        var vs = Analyze(src);
        MustHave(vs, "ExcessiveParameterList");
    }

    [Fact]
    public void TooManyFields_FiresOnLargeStruct()
    {
        // 16 fields (in groups of 8) > default threshold of 15
        var src = @"
public struct Big {
    public int A, B, C, D, E, F, G, H;
    public int I, J, K, L, M, N, O, P;
}";
        var vs = Analyze(src);
        MustHave(vs, "TooManyFields");
    }

    [Fact]
    public void TooManyFields_NoFireBelowThreshold()
    {
        // 15 fields = threshold, should NOT fire (strictly greater)
        var fields = string.Join("\n    ",
            Enumerable.Range(1, 15).Select(i => $"public int F{i};"));
        var src = $@"
public class Borderline {{
    {fields}
}}";
        var vs = Analyze(src);
        MustNotHave(vs, "TooManyFields");
    }

    // -----------------------------------------------------------------------
    // NPathComplexity
    // -----------------------------------------------------------------------

    [Fact]
    public void NPathComplexity_FiresOnHighNPath()
    {
        // Each sequential if (no else) contributes NP=2 to a product.
        // 8 sequential independent ifs with no shared else give NPath = 2^8 = 256 > 200.
        var src = @"
public class Foo {
    public void Bar(int a, int b, int c, int d, int e, int f, int g, int h) {
        if (a > 0) { int x = 1; }
        if (b > 0) { int x = 2; }
        if (c > 0) { int x = 3; }
        if (d > 0) { int x = 4; }
        if (e > 0) { int x = 5; }
        if (f > 0) { int x = 6; }
        if (g > 0) { int x = 7; }
        if (h > 0) { int x = 8; }
    }
}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<NPathComplexityRule>(
            new Dictionary<string, string> { ["minimum"] = "200" });
        var vs = Engine.Analyze(sf, new[] { set });
        MustHave(vs, "NPathComplexity");
    }

    [Fact]
    public void NPathComplexity_NoFireOnSimpleMethod()
    {
        var src = @"
public class Foo {
    public int Add(int a, int b) { return a + b; }
}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<NPathComplexityRule>(
            new Dictionary<string, string> { ["minimum"] = "200" });
        var vs = Engine.Analyze(sf, new[] { set });
        MustNotHave(vs, "NPathComplexity");
    }

    // -----------------------------------------------------------------------
    // ExcessiveMethodLength
    // -----------------------------------------------------------------------

    [Fact]
    public void ExcessiveMethodLength_FiresOnLongMethod()
    {
        // Build a method with 101 lines (threshold is 100, fires at >= 100)
        var lines = string.Join("\n        ",
            Enumerable.Range(0, 99).Select(i => $"int x{i} = {i};"));
        var src = $@"
public class Foo {{
    public void LongMethod() {{
        {lines}
    }}
}}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<ExcessiveMethodLengthRule>(
            new Dictionary<string, string> { ["minimum"] = "100", ["ignore-whitespace"] = "false" });
        var vs = Engine.Analyze(sf, new[] { set });
        MustHave(vs, "ExcessiveMethodLength");
    }

    [Fact]
    public void ExcessiveMethodLength_NoFireOnShortMethod()
    {
        var src = @"
public class Foo {
    public void Short() {
        int x = 1;
    }
}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<ExcessiveMethodLengthRule>(
            new Dictionary<string, string> { ["minimum"] = "100" });
        var vs = Engine.Analyze(sf, new[] { set });
        MustNotHave(vs, "ExcessiveMethodLength");
    }

    // -----------------------------------------------------------------------
    // ExcessiveClassLength
    // -----------------------------------------------------------------------

    [Fact]
    public void ExcessiveClassLength_FiresOnLongClass()
    {
        // Build a class with enough lines to exceed 1000
        var lines = string.Join("\n    ",
            Enumerable.Range(0, 998).Select(i => $"// line {i}"));
        var src = $@"
public class BigClass {{
    {lines}
    public void Placeholder() {{ }}
}}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<ExcessiveClassLengthRule>(
            new Dictionary<string, string> { ["minimum"] = "1000" });
        var vs = Engine.Analyze(sf, new[] { set });
        MustHave(vs, "ExcessiveClassLength");
    }

    [Fact]
    public void ExcessiveClassLength_NoFireOnSmallClass()
    {
        var src = @"
public class Small {
    public void Foo() {}
}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<ExcessiveClassLengthRule>(
            new Dictionary<string, string> { ["minimum"] = "1000" });
        var vs = Engine.Analyze(sf, new[] { set });
        MustNotHave(vs, "ExcessiveClassLength");
    }

    // -----------------------------------------------------------------------
    // ExcessivePublicCount
    // -----------------------------------------------------------------------

    [Fact]
    public void ExcessivePublicCount_FiresWhenOver45()
    {
        // 46 public fields >= threshold of 45 → fires
        var fields = string.Join("\n    ",
            Enumerable.Range(1, 46).Select(i => $"public int F{i};"));
        var src = $@"
public class BigPublic {{
    {fields}
}}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<ExcessivePublicCountRule>(
            new Dictionary<string, string> { ["minimum"] = "45" });
        var vs = Engine.Analyze(sf, new[] { set });
        MustHave(vs, "ExcessivePublicCount");
    }

    [Fact]
    public void ExcessivePublicCount_NoFireOnPrivateMembers()
    {
        var fields = string.Join("\n    ",
            Enumerable.Range(1, 50).Select(i => $"private int F{i};"));
        var src = $@"
public class AllPrivate {{
    {fields}
}}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<ExcessivePublicCountRule>(
            new Dictionary<string, string> { ["minimum"] = "45" });
        var vs = Engine.Analyze(sf, new[] { set });
        MustNotHave(vs, "ExcessivePublicCount");
    }

    // -----------------------------------------------------------------------
    // TooManyMethods
    // -----------------------------------------------------------------------

    [Fact]
    public void TooManyMethods_FiresAboveThreshold()
    {
        // 26 non-getter methods > default threshold of 25
        var methods = string.Join("\n    ",
            Enumerable.Range(1, 26).Select(i => $"public void Do{i}() {{}}"));
        var src = $@"
public class Busy {{
    {methods}
}}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<TooManyMethodsRule>(
            new Dictionary<string, string>
            {
                ["maxmethods"] = "25",
                ["ignorepattern"] = "(^(set|get|is|has|with))i",
            });
        var vs = Engine.Analyze(sf, new[] { set });
        MustHave(vs, "TooManyMethods");
    }

    [Fact]
    public void TooManyMethods_IgnoresGetterAndSetterByDefault()
    {
        // 10 Do methods + 20 getter-named methods; only 10 counted after filter
        var doMethods = string.Join("\n    ",
            Enumerable.Range(1, 10).Select(i => $"public void Do{i}() {{}}"));
        var getMethods = string.Join("\n    ",
            Enumerable.Range(1, 20).Select(i => $"public int GetValue{i}() {{ return {i}; }}"));
        var src = $@"
public class WithGetters {{
    {doMethods}
    {getMethods}
}}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<TooManyMethodsRule>(
            new Dictionary<string, string>
            {
                ["maxmethods"] = "25",
                ["ignorepattern"] = "(^(set|get|is|has|with))i",
            });
        var vs = Engine.Analyze(sf, new[] { set });
        MustNotHave(vs, "TooManyMethods");
    }

    // -----------------------------------------------------------------------
    // TooManyPublicMethods
    // -----------------------------------------------------------------------

    [Fact]
    public void TooManyPublicMethods_FiresAboveThreshold()
    {
        // 11 public non-accessor methods > default threshold of 10
        var methods = string.Join("\n    ",
            Enumerable.Range(1, 11).Select(i => $"public void Run{i}() {{}}"));
        var src = $@"
public class Running {{
    {methods}
}}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<TooManyPublicMethodsRule>(
            new Dictionary<string, string>
            {
                ["maxmethods"] = "10",
                ["ignorepattern"] = "(^(set|get|is|has|with))i",
            });
        var vs = Engine.Analyze(sf, new[] { set });
        MustHave(vs, "TooManyPublicMethods");
    }

    [Fact]
    public void TooManyPublicMethods_PrivateMethodsNotCounted()
    {
        // 20 private methods + 5 public → no violation
        var privates = string.Join("\n    ",
            Enumerable.Range(1, 20).Select(i => $"private void Private{i}() {{}}"));
        var publics = string.Join("\n    ",
            Enumerable.Range(1, 5).Select(i => $"public void Public{i}() {{}}"));
        var src = $@"
public class Mixed {{
    {privates}
    {publics}
}}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<TooManyPublicMethodsRule>(
            new Dictionary<string, string>
            {
                ["maxmethods"] = "10",
                ["ignorepattern"] = "(^(set|get|is|has|with))i",
            });
        var vs = Engine.Analyze(sf, new[] { set });
        MustNotHave(vs, "TooManyPublicMethods");
    }

    // -----------------------------------------------------------------------
    // ExcessiveClassComplexity (WeightedMethodCount)
    // -----------------------------------------------------------------------

    [Fact]
    public void ExcessiveClassComplexity_FiresWhenWmcExceedsThreshold()
    {
        // Build 10 methods each with CCN ~6 (5 ifs + base 1) = WMC 60 > 50
        var methods = string.Join("\n    ", Enumerable.Range(1, 10).Select(i => $@"
    public int M{i}(int a, int b, int c, int d, int e) {{
        int x = 0;
        if (a > 0) x++;
        if (b > 0) x++;
        if (c > 0) x++;
        if (d > 0) x++;
        if (e > 0) x++;
        return x;
    }}"));
        var src = $@"
public class Heavy {{
    {methods}
}}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<ExcessiveClassComplexityRule>(
            new Dictionary<string, string> { ["maximum"] = "50" });
        var vs = Engine.Analyze(sf, new[] { set });
        MustHave(vs, "ExcessiveClassComplexity");
    }

    [Fact]
    public void ExcessiveClassComplexity_NoFireOnSimpleClass()
    {
        var src = @"
public class Simple {
    public int Add(int a, int b) { return a + b; }
    public int Sub(int a, int b) { return a - b; }
}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<ExcessiveClassComplexityRule>(
            new Dictionary<string, string> { ["maximum"] = "50" });
        var vs = Engine.Analyze(sf, new[] { set });
        MustNotHave(vs, "ExcessiveClassComplexity");
    }

    // -----------------------------------------------------------------------
    // Exact message rendering for 3 rules
    // -----------------------------------------------------------------------

    [Fact]
    public void ExcessiveParameterList_RenderedMessage_MatchesTemplate()
    {
        var src = @"
public class Foo {
    public void ManyParams(int a, int b, int c, int d, int e,
        int f, int g, int h, int i, int j, int k) {}
}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<ExcessiveParameterListRule>(
            new Dictionary<string, string> { ["minimum"] = "10" });
        var vs = Engine.Analyze(sf, new[] { set });
        Assert.Single(vs);
        // "The method ManyParams has 11 parameters. Consider reducing the number of parameters to less than 10."
        Assert.Matches(
            @"^The method ManyParams has 11 parameters\. Consider reducing the number of parameters to less than 10\.$",
            vs[0].Description);
    }

    [Fact]
    public void TooManyFields_RenderedMessage_MatchesTemplate()
    {
        var fields = string.Join("\n    ",
            Enumerable.Range(1, 16).Select(i => $"public int F{i};"));
        var src = $@"
public class BigStruct {{
    {fields}
}}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<TooManyFieldsRule>(
            new Dictionary<string, string> { ["maxfields"] = "15" });
        var vs = Engine.Analyze(sf, new[] { set });
        Assert.Single(vs);
        // "The class BigStruct has 16 fields. Consider redesigning BigStruct to keep the number of fields under 15."
        Assert.Matches(
            @"^The class BigStruct has 16 fields\. Consider redesigning BigStruct to keep the number of fields under 15\.$",
            vs[0].Description);
    }

    [Fact]
    public void ExcessiveClassComplexity_RenderedMessage_MatchesTemplate()
    {
        var methods = string.Join("\n    ", Enumerable.Range(1, 10).Select(i => $@"
    public int M{i}(int a, int b, int c, int d, int e) {{
        int x = 0;
        if (a > 0) x++;
        if (b > 0) x++;
        if (c > 0) x++;
        if (d > 0) x++;
        if (e > 0) x++;
        return x;
    }}"));
        var src = $@"
public class Heavy {{
    {methods}
}}";
        var sf = ModelBuilder.Parse("fixture.cs", src);
        var set = BuildSingleRule<ExcessiveClassComplexityRule>(
            new Dictionary<string, string> { ["maximum"] = "50" });
        var vs = Engine.Analyze(sf, new[] { set });
        Assert.Single(vs);
        // "The class Heavy has an overall complexity of 60 which is very high. The configured complexity threshold is 50."
        Assert.Matches(
            @"^The class Heavy has an overall complexity of \d+ which is very high\. The configured complexity threshold is 50\.$",
            vs[0].Description);
    }

    // -----------------------------------------------------------------------
    // CodeSizeRules.All contains all 10 rules
    // -----------------------------------------------------------------------

    [Fact]
    public void CodeSizeRules_All_Contains10Rules()
    {
        Assert.Equal(10, CodeSizeRules.All.Count);
    }
}
