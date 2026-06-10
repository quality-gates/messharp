using MessCS.RuleSet;
using Xunit;

namespace MessCS.Tests;

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
            if (cc is MessCS.Rule.BaseRule br)
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
}
