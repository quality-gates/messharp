using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using MessSharp.Rules.Explicitness;
using Xunit;

namespace MessSharp.Tests;

public class ExplicitStateTests
{
    [Fact]
    public void ActiveSet_EnteringAnActiveItemFailsAndLeavesTheFirstEntryAlone()
    {
        var active = new ActiveSet<string>();
        using var outer = active.Enter("a");
        Assert.NotNull(outer);

        var again = active.Enter("a");
        again?.Dispose();

        Assert.Null(again);
        Assert.True(active.IsActive("a"));
    }

    [Fact]
    public void ActiveSet_DisposingTheScopeMakesTheItemIdleAgain()
    {
        var active = new ActiveSet<string>();
        using (active.Enter("a")) Assert.True(active.IsActive("a"));
        Assert.False(active.IsActive("a"));
        Assert.NotNull(active.Enter("a"));
    }

    private static void Work(ActiveSet<string> active)
    {
        using var scope = active.Enter("a");
        throw new InvalidOperationException();
    }

    [Fact]
    public void ActiveSet_LeavesTheItemIdleEvenWhenTheWorkThrows()
    {
        var active = new ActiveSet<string>();
        Assert.Throws<InvalidOperationException>(() => Work(active));
        Assert.False(active.IsActive("a"));
    }

    [Fact]
    public void Visited_IsTrueOnlyTheFirstTime()
    {
        var visited = new Visited<string>();
        Assert.True(visited.TryVisit("a"));
        Assert.False(visited.TryVisit("a"));
        Assert.True(visited.TryVisit("b"));
    }

    [Fact]
    public void Certainty_OneDoubtfulStepMakesItPossible()
    {
        Assert.True(Certainty.Definite.IsDefinite);
        Assert.True(Certainty.Definite.OrIf(false).IsDefinite);
        Assert.True(Certainty.Definite.OrIf(true).IsPossible);
        Assert.True(Certainty.Possible.OrIf(false).IsPossible);
    }

    [Theory]
    [InlineData("Created", false, "Forced")]
    [InlineData("Created", true, "MaybeForced")]
    [InlineData("MaybeForced", false, "Forced")]
    [InlineData("MaybeForced", true, "MaybeForced")]
    [InlineData("Forced", false, "Forced")]
    [InlineData("Forced", true, "Forced")]
    public void DeferredValue_OnlyMovesForward(string from, bool doubtful, string to)
    {
        var value = new StorageDeferredValue(DeferredKind.Lazy, [], Status: Enum.Parse<StorageDeferredStatus>(from));
        var certainty = doubtful ? Certainty.Possible : Certainty.Definite;
        Assert.Equal(to, value.AfterForcing(certainty).Status.ToString());
    }

    [Fact]
    public void RefTrail_ARepeatedRulesetIsACycleButSiblingsAreNot()
    {
        var a = RefTrail.Start.Enter("a");
        Assert.NotNull(a);
        Assert.Null(a.Enter("A"));
        Assert.NotNull(a.Enter("b"));
        Assert.NotNull(a.Enter("c"));
    }

    [Fact]
    public void RuleContext_FocusMovesPackageAndScopeTogetherAndFileFocusResets()
    {
        var file = ModelBuilder.Parse("t.cs", "namespace N { class C { void M() {} } }");
        var rule = new Loader().Load("codesize").SelectMany(s => s.Rules).First();
        var ctx = new RuleContext(file, rule, Properties.Empty, []);
        var cls = file.Classes.Single();

        ctx.FocusOn(cls.Namespace, cls.Node);
        Assert.Equal("N", ctx.CurrentPackage);
        Assert.Same(cls.Node, ctx.Scope);

        ctx.FocusOnFile();
        Assert.Equal(file.Namespace, ctx.CurrentPackage);
        Assert.Null(ctx.Scope);
    }

    [Fact]
    public void XmlRuleset_ADescriptionAfterTheFirstRuleIsNotTheRulesetDescription()
    {
        var xml = """
            <ruleset name="S"><description>head</description>
              <rule name="R" class="X"><description>rule text</description></rule>
              <description>late</description></ruleset>
            """;
        var parsed = XmlRulesetParser.Deserialize(System.Text.Encoding.UTF8.GetBytes(xml));
        Assert.Equal("head", parsed.Description);
        Assert.Equal("rule text", parsed.Rules![0].Description);
    }

    [Fact]
    public void XmlRuleset_ARuleEndingInATextChildDoesNotSwallowTheNextRule()
    {
        var xml = "<ruleset name=\"S\"><rule name=\"A\" class=\"X\"><priority>2</priority></rule>"
            + "<rule name=\"B\" class=\"X\"><description>d</description></rule><rule name=\"C\" class=\"X\"/></ruleset>";
        var parsed = XmlRulesetParser.Deserialize(System.Text.Encoding.UTF8.GetBytes(xml));
        Assert.Equal(["A", "B", "C"], parsed.Rules!.Select(r => r.Name));
    }

    [Fact]
    public void XmlRuleset_ARulesetDescriptionRightBeforeTheFirstRuleKeepsTheRule()
    {
        var xml = "<ruleset name=\"S\"><description>head</description><rule name=\"A\" class=\"X\"/></ruleset>";
        var parsed = XmlRulesetParser.Deserialize(System.Text.Encoding.UTF8.GetBytes(xml));
        Assert.Equal("head", parsed.Description);
        Assert.Equal(["A"], parsed.Rules!.Select(r => r.Name));
    }

    [Fact]
    public void Loader_ACyclicRefStillFailsAfterTheTrailChange()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "a.xml"), "<ruleset name=\"a\"><rule ref=\"b.xml\"/></ruleset>");
        File.WriteAllText(Path.Combine(dir, "b.xml"), "<ruleset name=\"b\"><rule ref=\"a.xml\"/></ruleset>");
        Assert.ThrowsAny<Exception>(() => new Loader().Load(Path.Combine(dir, "a.xml")));
    }
}
