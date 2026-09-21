using MessSharp.Model;
using MessSharp.Rule;
using Xunit;

namespace MessSharp.Tests;

/// <summary>
/// Behavioral tests for Engine's deep, source-level entry points.
/// Each test drives the production pipeline (parsing, dispatch, suppression)
/// through a single public method call, mirroring the seam the issue asks for.
/// </summary>
public class EngineSourceTests
{
    [Fact]
    public void Analyze_FromSource_FiresMethodRule()
    {
        const string source = @"
public class Foo {
    public void Bar(bool flag) { }
}";
        var rule = new NamedMethodRule("BooleanArgumentFlag");

        var violations = Engine.Analyze(source, new IRule[] { rule });

        Assert.NotEmpty(violations);
        Assert.All(violations, v => Assert.Equal("BooleanArgumentFlag", v.Rule.Name));
    }

    [Fact]
    public void Analyze_FromSourceFile_FiresMethodRule()
    {
        const string source = @"
public class Foo {
    public void Bar(bool flag) { }
}";
        var sf = ModelBuilder.Parse("test.cs", source);
        var rule = new NamedMethodRule("BooleanArgumentFlag");

        var violations = Engine.Analyze(sf, new IRule[] { rule });

        Assert.NotEmpty(violations);
        Assert.All(violations, v => Assert.Equal("BooleanArgumentFlag", v.Rule.Name));
    }

    [Fact]
    public void Analyze_FromSource_DispatchesFileRule()
    {
        const string source = @"
public class Foo {
    public void Bar() { }
}";
        var violations = Engine.Analyze(source, new IRule[] { new CountingFileRule() });

        Assert.Single(violations);
        Assert.Equal("CountingFileRule", violations[0].Rule.Name);
    }

    [Fact]
    public void Analyze_FromSource_DispatchesClassRule()
    {
        const string source = @"
public class Foo {
    public void Bar() { }
}";
        var violations = Engine.Analyze(source, new IRule[] { new CountingClassRule() });

        Assert.Single(violations);
        Assert.Equal("CountingClassRule", violations[0].Rule.Name);
    }

    [Fact]
    public void Analyze_FromSource_DispatchesInterfaceRule()
    {
        const string source = @"
public interface IFoo {
    void Bar();
}";
        var violations = Engine.Analyze(source, new IRule[] { new CountingInterfaceRule() });

        Assert.Single(violations);
        Assert.Equal("CountingInterfaceRule", violations[0].Rule.Name);
    }

    [Fact]
    public void Analyze_FromSource_SuppressesCommentSuppressedMethodWhenNotStrict()
    {
        const string source = @"
public class Foo {
    // @SuppressWarnings(PHPMD.BooleanArgumentFlag)
    public void Bar(bool flag) { }
}";
        var violations = Engine.Analyze(source, new IRule[] { new NamedMethodRule("BooleanArgumentFlag") });

        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_FromSource_ReportsSuppressedWhenStrict()
    {
        const string source = @"
public class Foo {
    // @SuppressWarnings(PHPMD.BooleanArgumentFlag)
    public void Bar(bool flag) { }
}";
        var violations = Engine.Analyze(source, new IRule[] { new NamedMethodRule("BooleanArgumentFlag") }, strict: true);

        Assert.Single(violations);
    }

    [Fact]
    public void Analyze_FromSource_RulePropertiesApply()
    {
        const string source = @"
public class Foo {
    public void Bar(bool flag) { }
}";
        var rule = new NamedMethodRule("BooleanArgumentFlag");
        rule.RuleProps = new Properties(new Dictionary<string, string>());

        var violations = Engine.Analyze(source, new IRule[] { rule });

        Assert.NotEmpty(violations);
    }

    // -------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------

    /// <summary>A minimal method rule that flags any method with parameters.</summary>
    private sealed class NamedMethodRule : BaseRule, IMethodRule
    {
        public NamedMethodRule(string name) => Name = name;

        public void Apply(RuleContext ctx, Model.MethodModel method)
        {
            if (method.Parameters.Count > 0)
                ctx.ReportMethod(method, method.Name);
        }
    }

    private sealed class CountingFileRule : BaseRule, IFileRule
    {
        public CountingFileRule() => Name = "CountingFileRule";

        public void Apply(RuleContext ctx) => ctx.Report(1, 1);
    }

    private sealed class CountingClassRule : BaseRule, IClassRule
    {
        public CountingClassRule() => Name = "CountingClassRule";

        public void Apply(RuleContext ctx, Model.ClassModel cls) => ctx.ReportClass(cls, cls.Name);
    }

    private sealed class CountingInterfaceRule : BaseRule, IInterfaceRule
    {
        public CountingInterfaceRule() => Name = "CountingInterfaceRule";

        public void Apply(RuleContext ctx, Model.InterfaceModel iface) => ctx.ReportInterface(iface, iface.Name);
    }
}
