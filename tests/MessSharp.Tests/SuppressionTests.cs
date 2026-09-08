using System.Diagnostics.CodeAnalysis;
using MessSharp.Cli;
using CliRunner = MessSharp.Cli.Cli;
using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.Rules.CodeSize;
using MessSharp.Rules.Controversial;
using MessSharp.Rules.Naming;
using MessSharp.Rules.UnusedCode;
using Xunit;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.Tests;

public class SuppressionTests
{
    private static RuleSetType MakeCodeSizeSet()
    {
        var rule = new CyclomaticComplexityRule
        {
            Name = "CyclomaticComplexity",
            Message = "The {0} {1}() has a Cyclomatic Complexity of {2}. The configured cyclomatic complexity threshold is {3}.",
            Priority = 3,
            SetName = "codesize",
            ExternalUrl = "https://phpmd.org/rules/codesize.html#cyclomaticcomplexity",
            Description = "CCN rule",
            Since = "0.1",
            RuleProps = new Properties(new Dictionary<string, string> { ["reportLevel"] = "10" }),
        };
        return new RuleSetType { Name = "codesize", Rules = { rule } };
    }

    private const string AttributeSuppressedMethodSource = @"
using System.Diagnostics.CodeAnalysis;

public class ComplexClass {
    [SuppressMessage(""MessSharp"", ""CyclomaticComplexity"")]
    [SuppressMessage(""MessSharp"", ""NPathComplexity"")]
    public int HeavyMethod(int a, int b, int c, int d, int e) {
        int x = 0;
        if (a > 0 && b > 0) { x++; }
        if (a > 1) { x++; }
        if (b > 1) { x++; }
        for (int i = 0; i < a; i++) { x++; }
        switch (c) {
            case 1: x++; break;
            case 2: x++; break;
            case 3: x++; break;
        }
        if (d > 0) { x++; }
        if (e > 0) { x++; }
        return x;
    }
}";

    private const string CommentSuppressedMethodSource = @"
public class ComplexClass {
    // @SuppressWarnings(PHPMD.CyclomaticComplexity)
    public int HeavyMethod(int a, int b, int c, int d, int e) {
        int x = 0;
        if (a > 0 && b > 0) { x++; }
        if (a > 1) { x++; }
        if (b > 1) { x++; }
        for (int i = 0; i < a; i++) { x++; }
        switch (c) {
            case 1: x++; break;
            case 2: x++; break;
            case 3: x++; break;
        }
        if (d > 0) { x++; }
        if (e > 0) { x++; }
        return x;
    }
}";

    private const string ClassLevelSuppressedSource = @"
using System.Diagnostics.CodeAnalysis;

[SuppressMessage(""MessSharp"", ""CyclomaticComplexity"")]
public class ComplexClass {
    public int HeavyMethod(int a, int b, int c, int d, int e) {
        int x = 0;
        if (a > 0 && b > 0) { x++; }
        if (a > 1) { x++; }
        if (b > 1) { x++; }
        for (int i = 0; i < a; i++) { x++; }
        switch (c) {
            case 1: x++; break;
            case 2: x++; break;
            case 3: x++; break;
        }
        if (d > 0) { x++; }
        if (e > 0) { x++; }
        return x;
    }
}";

    [Fact]
    public void Engine_SuppressedByAttribute_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("complex.cs", AttributeSuppressedMethodSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedByAttribute_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("complex.cs", AttributeSuppressedMethodSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    [Fact]
    public void Engine_SuppressedByComment_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("complex.cs", CommentSuppressedMethodSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedByComment_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("complex.cs", CommentSuppressedMethodSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    [Fact]
    public void Engine_SuppressedAtClassLevel_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("complex.cs", ClassLevelSuppressedSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    private const string AttributeSuppressedSecondOverloadSource = @"
using System.Diagnostics.CodeAnalysis;

public class ComplexClass {
    public int HeavyMethod(int a) {
        int x = 0;
        if (a > 0) { x++; }
        if (a > 1) { x++; }
        if (a > 2) { x++; }
        if (a > 3) { x++; }
        if (a > 4) { x++; }
        if (a > 5) { x++; }
        if (a > 6) { x++; }
        if (a > 7) { x++; }
        if (a > 8) { x++; }
        if (a > 9) { x++; }
        return x;
    }

    [SuppressMessage(""MessSharp"", ""CyclomaticComplexity"")]
    public int HeavyMethod(int a, int b) {
        int x = 0;
        if (a > 0) { x++; }
        if (a > 1) { x++; }
        if (a > 2) { x++; }
        if (a > 3) { x++; }
        if (a > 4) { x++; }
        if (a > 5) { x++; }
        if (a > 6) { x++; }
        if (a > 7) { x++; }
        if (a > 8) { x++; }
        if (a > 9) { x++; }
        return x;
    }
}";

    [Fact]
    public void Engine_SuppressedSecondOverloadByAttribute_DoesNotSuppressFirst()
    {
        var sf = ModelBuilder.Parse("complex.cs", AttributeSuppressedSecondOverloadSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);

        Assert.Single(violations);
        Assert.Equal(sf.AllMethods[0].Line, violations[0].BeginLine);
    }

    private const string CommentSuppressedFirstOverloadSource = @"
public class ComplexClass {
    // @SuppressWarnings(PHPMD.CyclomaticComplexity)
    public int HeavyMethod(int a) {
        int x = 0;
        if (a > 0) { x++; }
        if (a > 1) { x++; }
        if (a > 2) { x++; }
        if (a > 3) { x++; }
        if (a > 4) { x++; }
        if (a > 5) { x++; }
        if (a > 6) { x++; }
        if (a > 7) { x++; }
        if (a > 8) { x++; }
        if (a > 9) { x++; }
        return x;
    }

    public int HeavyMethod(int a, int b) {
        int x = 0;
        if (a > 0) { x++; }
        if (a > 1) { x++; }
        if (a > 2) { x++; }
        if (a > 3) { x++; }
        if (a > 4) { x++; }
        if (a > 5) { x++; }
        if (a > 6) { x++; }
        if (a > 7) { x++; }
        if (a > 8) { x++; }
        if (a > 9) { x++; }
        return x;
    }
}";

    [Fact]
    public void Engine_SuppressedFirstOverloadByComment_DoesNotSuppressSecond()
    {
        var sf = ModelBuilder.Parse("complex.cs", CommentSuppressedFirstOverloadSource);
        var sets = new[] { MakeCodeSizeSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);

        Assert.Single(violations);
        Assert.Equal(sf.AllMethods[1].Line, violations[0].BeginLine);
    }

    [Fact]
    public void Cli_WithSuppressedViolation_StrictFlagControlsExitCode()
    {
        var tmpFile = Path.GetTempFileName() + ".cs";
        File.WriteAllText(tmpFile, AttributeSuppressedMethodSource);
        var rulesetsDir = Path.Combine(AppContext.BaseDirectory, "rulesets");
        var codesizeXml = Path.Combine(rulesetsDir, "codesize.xml");
        try
        {
            var outW = new StringWriter();
            var errW = new StringWriter();
            int cleanCode = CliRunner.Run(new[] { tmpFile, "text", codesizeXml }, outW, errW);
            Assert.Equal(0, cleanCode);

            var outStrict = new StringWriter();
            var errStrict = new StringWriter();
            int strictCode = CliRunner.Run(new[] { tmpFile, "text", codesizeXml, "--strict" }, outStrict, errStrict);
            Assert.Equal(2, strictCode);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    private static RuleSetType MakeNamingSet()
    {
        var rule = new ConstantNamingConventionsRule
        {
            Name = "ConstantNamingConventions",
            Message = "Constant {0} should be defined in PascalCase",
            Priority = 3,
            SetName = "naming",
        };
        return new RuleSetType { Name = "naming", Rules = { rule } };
    }

    private const string AttributeSuppressedConstantSource = @"
using System.Diagnostics.CodeAnalysis;

public class Sample
{
    [SuppressMessage(""MessSharp"", ""ConstantNamingConventions"")]
    public const int legacy_const_value = 42;
}";

    private const string CommentSuppressedConstantSource = @"
public class Sample
{
    // @SuppressWarnings(PHPMD.ConstantNamingConventions)
    public const int legacy_const_value = 42;
}";

    [Fact]
    public void Engine_SuppressedConstantByAttribute_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", AttributeSuppressedConstantSource);
        var sets = new[] { MakeNamingSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedConstantByAttribute_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", AttributeSuppressedConstantSource);
        var sets = new[] { MakeNamingSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    [Fact]
    public void Engine_SuppressedConstantByComment_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", CommentSuppressedConstantSource);
        var sets = new[] { MakeNamingSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedConstantByComment_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", CommentSuppressedConstantSource);
        var sets = new[] { MakeNamingSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    private const string UnsuppressedConstantSource = @"
public class Sample
{
    public const int legacy_const_value = 42;
}";

    [Fact]
    public void Engine_UnsuppressedConstant_ReportedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", UnsuppressedConstantSource);
        var sets = new[] { MakeNamingSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Single(violations);
    }

    private static RuleSetType MakeControversialSet()
    {
        var rule = new CamelCasePropertyNameRule
        {
            Name = "CamelCasePropertyName",
            Message = "Field {0} should be defined in camelCase",
            Priority = 3,
            SetName = "controversial",
        };
        return new RuleSetType { Name = "controversial", Rules = { rule } };
    }

    private const string AttributeSuppressedFieldSource = @"
using System.Diagnostics.CodeAnalysis;

public class Sample
{
    [SuppressMessage(""MessSharp"", ""CamelCasePropertyName"")]
    private int PascalCaseField = 42;
}";

    private const string CommentSuppressedFieldSource = @"
public class Sample
{
    // @SuppressWarnings(PHPMD.CamelCasePropertyName)
    private int PascalCaseField = 42;
}";

    [Fact]
    public void Engine_SuppressedFieldByAttribute_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", AttributeSuppressedFieldSource);
        var sets = new[] { MakeControversialSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedFieldByAttribute_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", AttributeSuppressedFieldSource);
        var sets = new[] { MakeControversialSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    [Fact]
    public void Engine_SuppressedFieldByComment_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", CommentSuppressedFieldSource);
        var sets = new[] { MakeControversialSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedFieldByComment_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", CommentSuppressedFieldSource);
        var sets = new[] { MakeControversialSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    private static RuleSetType MakeUnusedCodeSet()
    {
        var rule = new UnusedPrivateFieldRule
        {
            Name = "UnusedPrivateField",
            Message = "Avoid unused private fields such as '{0}'.",
            Priority = 3,
            SetName = "unusedcode",
        };
        return new RuleSetType { Name = "unusedcode", Rules = { rule } };
    }

    private const string AttributeSuppressedUnusedFieldSource = @"
using System.Diagnostics.CodeAnalysis;

public class Sample
{
    private int _used = 1;

    [SuppressMessage(""MessSharp"", ""UnusedPrivateField"")]
    private int _ignored;

    public int Get() => _used;
}";

    private const string CommentSuppressedUnusedFieldSource = @"
public class Sample
{
    private int _used = 1;

    // @SuppressWarnings(PHPMD.UnusedPrivateField)
    private int _ignored;

    public int Get() => _used;
}";

    [Fact]
    public void Engine_SuppressedUnusedPrivateFieldByAttribute_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", AttributeSuppressedUnusedFieldSource);
        var sets = new[] { MakeUnusedCodeSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedUnusedPrivateFieldByAttribute_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", AttributeSuppressedUnusedFieldSource);
        var sets = new[] { MakeUnusedCodeSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    [Fact]
    public void Engine_SuppressedUnusedPrivateFieldByComment_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", CommentSuppressedUnusedFieldSource);
        var sets = new[] { MakeUnusedCodeSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedUnusedPrivateFieldByComment_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", CommentSuppressedUnusedFieldSource);
        var sets = new[] { MakeUnusedCodeSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    private const string ClassLevelSuppressedUnusedFieldSource = @"
using System.Diagnostics.CodeAnalysis;

[SuppressMessage(""MessSharp"", ""UnusedPrivateField"")]
public class Sample
{
    private int _used = 1;
    private int _ignored;

    public int Get() => _used;
}";

    [Fact]
    public void Engine_SuppressedUnusedPrivateFieldAtClassLevel_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", ClassLevelSuppressedUnusedFieldSource);
        var sets = new[] { MakeUnusedCodeSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    private static RuleSetType MakeCamelCaseClassNameSet()
    {
        var rule = new CamelCaseClassNameRule
        {
            Name = "CamelCaseClassName",
            Message = "The {0} {1} is not named in PascalCase",
            Priority = 1,
            SetName = "controversial",
        };
        return new RuleSetType { Name = "controversial", Rules = { rule } };
    }

    private static RuleSetType MakeCamelCaseMethodNameSet()
    {
        var rule = new CamelCaseMethodNameRule
        {
            Name = "CamelCaseMethodName",
            Message = "The method {0}() is not named in PascalCase",
            Priority = 1,
            SetName = "controversial",
        };
        return new RuleSetType { Name = "controversial", Rules = { rule } };
    }

    private const string AttributeSuppressedInterfaceSource = @"
using System.Diagnostics.CodeAnalysis;

[SuppressMessage(""MessSharp"", ""CamelCaseClassName"")]
public interface I_Bad_Name
{
}";

    private const string CommentSuppressedInterfaceSource = @"
public class PlainClass
{
}

// @SuppressWarnings(PHPMD.CamelCaseClassName)
public interface I_Bad_Name
{
}";

    [Fact]
    public void Engine_SuppressedInterfaceByAttribute_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", AttributeSuppressedInterfaceSource);
        var sets = new[] { MakeCamelCaseClassNameSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedInterfaceByAttribute_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", AttributeSuppressedInterfaceSource);
        var sets = new[] { MakeCamelCaseClassNameSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    [Fact]
    public void Engine_SuppressedInterfaceByComment_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", CommentSuppressedInterfaceSource);
        var sets = new[] { MakeCamelCaseClassNameSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedInterfaceByComment_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", CommentSuppressedInterfaceSource);
        var sets = new[] { MakeCamelCaseClassNameSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    private const string AttributeSuppressedInterfaceMethodSource = @"
using System.Diagnostics.CodeAnalysis;

public interface IGoodName
{
    [SuppressMessage(""MessSharp"", ""CamelCaseMethodName"")]
    void Do_Thing();
}";

    private const string CommentSuppressedInterfaceMethodSource = @"
public interface IGoodName
{
    // @SuppressWarnings(PHPMD.CamelCaseMethodName)
    void Do_Thing();
}";

    [Fact]
    public void Engine_SuppressedInterfaceMethodByAttribute_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", AttributeSuppressedInterfaceMethodSource);
        var sets = new[] { MakeCamelCaseMethodNameSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedInterfaceMethodByAttribute_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", AttributeSuppressedInterfaceMethodSource);
        var sets = new[] { MakeCamelCaseMethodNameSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    [Fact]
    public void Engine_SuppressedInterfaceMethodByComment_SuppressedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", CommentSuppressedInterfaceMethodSource);
        var sets = new[] { MakeCamelCaseMethodNameSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Engine_SuppressedInterfaceMethodByComment_ReportedWhenStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", CommentSuppressedInterfaceMethodSource);
        var sets = new[] { MakeCamelCaseMethodNameSet() };
        var violations = Engine.Analyze(sf, sets, strict: true);
        Assert.Single(violations);
    }

    private const string UnsuppressedInterfaceSource = @"
public interface I_Bad_Name
{
}";

    private const string UnsuppressedInterfaceMethodSource = @"
public interface IGoodName
{
    void Do_Thing();
}";

    [Fact]
    public void Engine_UnsuppressedInterface_ReportedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", UnsuppressedInterfaceSource);
        var sets = new[] { MakeCamelCaseClassNameSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Single(violations);
    }

    [Fact]
    public void Engine_UnsuppressedInterfaceMethod_ReportedWhenNotStrict()
    {
        var sf = ModelBuilder.Parse("sample.cs", UnsuppressedInterfaceMethodSource);
        var sets = new[] { MakeCamelCaseMethodNameSet() };
        var violations = Engine.Analyze(sf, sets, strict: false);
        Assert.Single(violations);
    }
}
