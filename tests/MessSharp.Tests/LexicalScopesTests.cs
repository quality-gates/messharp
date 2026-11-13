using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

/// <summary>One scope resolver answers "is this name a local?" for the design, explicitness and unused-code rules (#220).</summary>
public class LexicalScopesTests
{
    private static List<string> Rules(string body, string ruleset)
    {
        var source = $$"""
            using System;
            public class Probe {
                private static object v;
                public static int Run() {
                    {{body}}
                    return 0;
                }
            }
            """;
        return Engine.Analyze(source, new Loader().Load(ruleset).SelectMany(set => set.Rules).ToArray())
            .Select(violation => violation.Rule.Name).Distinct().ToList();
    }

    [Theory]
    [InlineData("try { } catch (Exception v) { Console.WriteLine(v); v = null; }")]
    [InlineData("Action<object> a = v => { Console.WriteLine(v); v = null; };")]
    [InlineData("Action<object> a = (v) => { Console.WriteLine(v); v = null; };")]
    [InlineData("Action<object> a = delegate (object v) { Console.WriteLine(v); v = null; };")]
    [InlineData("void Local(object v) { Console.WriteLine(v); v = null; }")]
    [InlineData("object v = 1; Console.WriteLine(v); v = null;")]
    [InlineData("foreach (var v in new object[0]) { Console.WriteLine(v); }")]
    [InlineData("var q = from v in new object[0] select v;")]
    public void A_local_binding_shadows_the_static_field_for_design_and_unused_code(string body)
    {
        Assert.DoesNotContain("GlobalVariable", Rules(body, "design"));
        Assert.Contains("UnusedPrivateField", Rules(body, "unusedcode"));
    }

    [Fact]
    public void Control_an_unshadowed_use_is_not_hidden()
    {
        Assert.Contains("GlobalVariable", Rules("v = null;", "design"));
        Assert.DoesNotContain("UnusedPrivateField", Rules("Console.WriteLine(v);", "unusedcode"));
    }

    [Fact]
    public void A_foreach_variable_shadows_only_its_own_statement()
    {
        const string body = "Console.WriteLine(v); foreach (var v in new object[0]) { }";
        Assert.DoesNotContain("UnusedPrivateField", Rules(body, "unusedcode"));
    }

    [Fact]
    public void An_indexer_parameter_shadows_the_field_inside_the_indexer()
    {
        var source = "public class Probe { private int v; public int this[int v] => v; }";
        var rules = Engine.Analyze(source, new Loader().Load("unusedcode").SelectMany(set => set.Rules).ToArray())
            .Select(violation => violation.Rule.Name);
        Assert.Contains("UnusedPrivateField", rules);
    }
}
