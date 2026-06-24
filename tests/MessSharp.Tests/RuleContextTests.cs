using MessSharp.Rule;
using Xunit;

namespace MessSharp.Tests;

public class RuleContextTests
{
    [Theory]
    [InlineData("Hello {0}", new object[] { "world" }, "Hello world")]
    [InlineData("Value is {0}", new object[] { 42 }, "Value is 42")]
    [InlineData("Float {0}", new object[] { 10.0 }, "Float 10")]
    [InlineData("Float {0}", new object[] { 10.5 }, "Float 10.5")]
    [InlineData("Multi {0} and {1}", new object[] { "a", "b" }, "Multi a and b")]
    [InlineData("No placeholders", new object[] { }, "No placeholders")]
    [InlineData("Out of range {2}", new object[] { "x" }, "Out of range {2}")]
    public void RenderMessage_SubstitutesPlaceholders(string tmpl, object[] args, string expected)
    {
        Assert.Equal(expected, RuleContext.RenderMessage(tmpl, args));
    }

    [Fact]
    public void RenderMessage_IntegralDouble_NoPeriod()
    {
        var result = RuleContext.RenderMessage("{0}", new object[] { 50.0 });
        Assert.Equal("50", result);
    }

    [Fact]
    public void CompileRegex_PhpmdPattern_CompilesSuccessfully()
    {
        var re = RuleContext.CompileRegex("(^(get|set))i");
        Assert.NotNull(re);
        Assert.Matches(re!, "getName");
        Assert.Matches(re!, "SetValue");
    }

    [Fact]
    public void CompileRegex_EmptyString_ReturnsNull()
    {
        Assert.Null(RuleContext.CompileRegex(""));
    }

    [Fact]
    public void CompileRegex_PlainPattern_CompilesSuccessfully()
    {
        var re = RuleContext.CompileRegex("foo");
        Assert.NotNull(re);
        Assert.Matches(re!, "foobar");
    }

    [Fact]
    public void SortViolations_SortsByFileThenLine()
    {
        var violations = new List<Violation>
        {
            MakeViolation("b.cs", 5),
            MakeViolation("a.cs", 10),
            MakeViolation("a.cs", 3),
            MakeViolation("b.cs", 1),
        };
        RuleContext.SortViolations(violations);
        Assert.Equal("a.cs", violations[0].File);
        Assert.Equal(3, violations[0].BeginLine);
        Assert.Equal("a.cs", violations[1].File);
        Assert.Equal(10, violations[1].BeginLine);
        Assert.Equal("b.cs", violations[2].File);
        Assert.Equal(1, violations[2].BeginLine);
    }

    private static Violation MakeViolation(string file, int line) =>
        new Violation
        {
            Rule = new StubRule(),
            File = file,
            BeginLine = line,
        };

    private sealed class StubRule : IRule
    {
        public string Name => "Stub";
        public string Message => "{0}";
        public int Priority => 3;
        public string SetName => "test";
        public string ExternalUrl => "";
        public string Description => "";
        public string Since => "";
    }
}
