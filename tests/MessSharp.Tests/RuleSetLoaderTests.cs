using MessSharp.RuleSet;
using Xunit;

namespace MessSharp.Tests;

public class RuleSetLoaderTests
{
    private static string RulesetsDir => Path.Combine(AppContext.BaseDirectory, "rulesets");

    [Fact]
    public void Load_CodeSize_LoadsCyclomaticComplexityRule()
    {
        var loader = new Loader { MaxPriority = 1 };
        var sets = loader.Load(Path.Combine(RulesetsDir, "codesize.xml"));
        Assert.Single(sets);
        var set = sets[0];
        Assert.Equal("Code Size Rules", set.Name);
        var cc = set.Rules.FirstOrDefault(r => r.Name == "CyclomaticComplexity");
        Assert.NotNull(cc);
        Assert.Equal(3, cc!.Priority);
    }

    [Fact]
    public void Load_WithPropertyOverride_AppliesOverride()
    {
        // Create a custom ruleset XML that refs codesize/CyclomaticComplexity
        // with a property override.
        var xmlContent = $@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<ruleset name=""Custom"">
  <description>Custom</description>
  <rule ref=""{Path.Combine(RulesetsDir, "codesize.xml")}/CyclomaticComplexity"">
    <priority>1</priority>
    <properties>
      <property name=""reportLevel"" value=""5""/>
    </properties>
  </rule>
</ruleset>";
        var tmpFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(tmpFile, xmlContent);
        try
        {
            var loader = new Loader { MaxPriority = 1 };
            var sets = loader.Load(tmpFile);
            Assert.Single(sets);
            var cc = sets[0].Rules.FirstOrDefault(r => r.Name == "CyclomaticComplexity");
            Assert.NotNull(cc);
            Assert.Equal(1, cc!.Priority);
            if (cc is MessSharp.Rule.BaseRule br)
                Assert.Equal(5, br.RuleProps.Int("reportLevel", 10));
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public void Load_WithExclude_ExcludesRule()
    {
        var xmlContent = $@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<ruleset name=""Custom"">
  <description>Custom</description>
  <rule ref=""{Path.Combine(RulesetsDir, "codesize.xml")}"">
    <exclude name=""CyclomaticComplexity""/>
  </rule>
</ruleset>";
        var tmpFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(tmpFile, xmlContent);
        try
        {
            var loader = new Loader { MaxPriority = 1 };
            var sets = loader.Load(tmpFile);
            var cc = sets.SelectMany(s => s.Rules).FirstOrDefault(r => r.Name == "CyclomaticComplexity");
            Assert.Null(cc);
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public void FilterRules_Enable_KeepsOnlyNamed()
    {
        var loader = new Loader { MaxPriority = 1 };
        var sets = loader.Load(Path.Combine(RulesetsDir, "codesize.xml"));
        Loader.FilterRules(sets, new[] { "CyclomaticComplexity" }, Array.Empty<string>());
        Assert.All(sets.SelectMany(s => s.Rules), r => Assert.Equal("CyclomaticComplexity", r.Name));
    }

    [Fact]
    public void FilterRules_Disable_RemovesNamed()
    {
        var loader = new Loader { MaxPriority = 1 };
        var sets = loader.Load(Path.Combine(RulesetsDir, "codesize.xml"));
        var before = sets.SelectMany(s => s.Rules).Count();
        Loader.FilterRules(sets, Array.Empty<string>(), new[] { "CyclomaticComplexity" });
        var after = sets.SelectMany(s => s.Rules).Count();
        Assert.Equal(before - 1, after);
    }

    [Fact]
    public void Load_ReferencingCSharpRuleset_LoadsSameRulesAsCSharpDirectly()
    {
        var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<ruleset name=""Custom"">
  <description>Custom ruleset referencing csharp</description>
  <rule ref=""csharp""/>
</ruleset>";
        var tmpFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(tmpFile, xmlContent);
        try
        {
            var direct = new Loader { MaxPriority = 1 }.Load("csharp");
            var indirect = new Loader { MaxPriority = 1 }.Load(tmpFile);
            Assert.NotEmpty(indirect[0].Rules);
            Assert.Equal(direct[0].Rules.Count, indirect[0].Rules.Count);
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public void Load_WithUnresolvableRulesetRef_ThrowsFileNotFoundException()
    {
        var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<ruleset name=""Custom"">
  <description>Custom ruleset with bad ref</description>
  <rule ref=""nosuchset/SomeRule""/>
</ruleset>";
        var tmpFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(tmpFile, xmlContent);
        try
        {
            var loader = new Loader { MaxPriority = 1 };
            var ex = Assert.Throws<FileNotFoundException>(() => loader.Load(tmpFile));
            Assert.Contains("Cannot resolve ref: nosuchset/SomeRule", ex.Message);
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public void Load_WithUnknownRuleRef_ThrowsInvalidOperationException()
    {
        var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<ruleset name=""Custom"">
  <description>Custom ruleset with unknown rule in existing set</description>
  <rule ref=""naming/NoSuchRule""/>
</ruleset>";
        var tmpFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(tmpFile, xmlContent);
        try
        {
            var loader = new Loader { MaxPriority = 1 };
            var ex = Assert.Throws<InvalidOperationException>(() => loader.Load(tmpFile));
            Assert.Contains("Cannot resolve rule: naming/NoSuchRule", ex.Message);
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public void Load_ReferencingRuleThroughNestedRuleset_LoadsRule()
    {
        var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<ruleset name=""Custom"">
  <description>Custom ruleset referencing rule via csharp</description>
  <rule ref=""csharp/CyclomaticComplexity""/>
</ruleset>";
        var tmpFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(tmpFile, xmlContent);
        try
        {
            var loader = new Loader { MaxPriority = 1 };
            var sets = loader.Load(tmpFile);
            Assert.Single(sets);
            var rule = sets[0].Rules.FirstOrDefault(r => r.Name == "CyclomaticComplexity");
            Assert.NotNull(rule);
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public void Load_BareRuleRef_ResolvesRule()
    {
        var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<ruleset name=""Custom"">
  <description>Custom ruleset referencing bare rule name</description>
  <rule ref=""LongVariable"">
    <priority>2</priority>
    <properties>
      <property name=""maximum"" value=""50""/>
    </properties>
  </rule>
</ruleset>";
        var tmpFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(tmpFile, xmlContent);
        try
        {
            var loader = new Loader { MaxPriority = 1 };
            var sets = loader.Load(tmpFile);
            Assert.Single(sets);
            var rule = sets[0].Rules.FirstOrDefault(r => r.Name == "LongVariable");
            Assert.NotNull(rule);
            Assert.Equal(2, rule!.Priority);
            if (rule is MessSharp.Rule.BaseRule br)
            {
                Assert.Equal(50, br.RuleProps.Int("maximum", 20));
            }
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public void Load_TeamPolicy_BareRuleRefOverridesPriorRule()
    {
        var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<ruleset name=""team policy"">
  <rule ref=""csharp"">
    <exclude name=""DevelopmentCodeFragment"" />
  </rule>
  <rule ref=""LongVariable"">
    <priority>2</priority>
    <properties>
      <property name=""maximum"" value=""50"" />
    </properties>
  </rule>
</ruleset>";
        var tmpFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(tmpFile, xmlContent);
        try
        {
            var loader = new Loader { MaxPriority = 1 };
            var sets = loader.Load(tmpFile);
            Assert.Single(sets);
            var rules = sets[0].Rules.Where(r => r.Name == "LongVariable").ToList();
            Assert.Single(rules);
            var rule = rules[0];
            Assert.Equal(2, rule.Priority);
            if (rule is MessSharp.Rule.BaseRule br)
            {
                Assert.Equal(50, br.RuleProps.Int("maximum", 20));
            }
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public void Load_TeamPolicy_QualifiedRuleRefOverridesPriorRule()
    {
        var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<ruleset name=""team policy"">
  <rule ref=""csharp"">
    <exclude name=""DevelopmentCodeFragment"" />
  </rule>
  <rule ref=""naming/LongVariable"">
    <priority>2</priority>
    <properties>
      <property name=""maximum"" value=""50"" />
    </properties>
  </rule>
</ruleset>";
        var tmpFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(tmpFile, xmlContent);
        try
        {
            var loader = new Loader { MaxPriority = 1 };
            var sets = loader.Load(tmpFile);
            Assert.Single(sets);
            var rules = sets[0].Rules.Where(r => r.Name == "LongVariable").ToList();
            Assert.Single(rules);
            var rule = rules[0];
            Assert.Equal(2, rule.Priority);
            if (rule is MessSharp.Rule.BaseRule br)
            {
                Assert.Equal(50, br.RuleProps.Int("maximum", 20));
            }
        }
        finally { File.Delete(tmpFile); }
    }

    [Fact]
    public void Load_TeamPolicy_CustomMessageAndDescription_OverridesDefaults()
    {
        var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<ruleset name=""team policy"">
  <rule ref=""csharp"">
    <exclude name=""DevelopmentCodeFragment"" />
  </rule>
  <rule ref=""naming/LongVariable"" message=""Custom long variable warning"">
    <description>Custom description for long variables</description>
    <priority>2</priority>
    <properties>
      <property name=""maximum"" value=""50"" />
    </properties>
  </rule>
</ruleset>";
        var tmpFile = Path.GetTempFileName() + ".xml";
        File.WriteAllText(tmpFile, xmlContent);
        try
        {
            var loader = new Loader { MaxPriority = 1 };
            var sets = loader.Load(tmpFile);
            Assert.Single(sets);
            var rules = sets[0].Rules.Where(r => r.Name == "LongVariable").ToList();
            Assert.Single(rules);
            var rule = rules[0];
            Assert.Equal("Custom long variable warning", rule.Message);
            Assert.Equal("Custom description for long variables", rule.Description);
            Assert.Equal(2, rule.Priority);
            if (rule is MessSharp.Rule.BaseRule br)
            {
                Assert.Equal(50, br.RuleProps.Int("maximum", 20));
            }
        }
        finally { File.Delete(tmpFile); }
    }
}

