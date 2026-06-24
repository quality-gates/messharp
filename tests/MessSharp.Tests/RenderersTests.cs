using System.Text;
using System.Text.Json;
using System.Xml;
using Xunit;
using MessSharp.Report;
using MessSharp.Rule;
using ViolationReport = MessSharp.Report.Report;

namespace MessSharp.Tests;

/// <summary>
/// Tests for all 9 report renderers (text + the 8 new ones).
/// Uses a small in-memory Report with 2 violations (in 2 different files)
/// and one processing error.
/// </summary>
public class RenderersTests
{
    // -------------------------------------------------------------------------
    // Fixture setup
    // -------------------------------------------------------------------------

    private static ViolationReport MakeReport()
    {
        var rule1 = new FakeRule
        {
            Name = "CyclomaticComplexity",
            SetName = "codesize",
            Priority = 2,
            ExternalUrl = "https://phpmd.org/rules/codesize.html#cyclomaticcomplexity",
            Description = "The method foo() has a Cyclomatic Complexity of 15.",
        };
        var rule2 = new FakeRule
        {
            Name = "TooManyFields",
            SetName = "codesize",
            Priority = 3,
            ExternalUrl = "https://phpmd.org/rules/codesize.html#toomanyfields",
            Description = "The class Bar has too many fields.",
        };
        var rule3 = new FakeRule
        {
            Name = "ShortVariable",
            SetName = "naming",
            Priority = 4,
            ExternalUrl = "https://phpmd.org/rules/naming.html#shortvariable",
            Description = "Avoid variables with short names like $x.",
        };

        return new ViolationReport
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Rule = rule1,
                    File = "/src/Foo.cs",
                    BeginLine = 10,
                    EndLine = 30,
                    Description = "The method foo() has a Cyclomatic Complexity of 15.",
                    Class = "Foo",
                    Method = "foo",
                    Package = "MyApp",
                    RuleSetName = "codesize",
                    Priority = 2,
                },
                new Violation
                {
                    Rule = rule2,
                    File = "/src/Bar.cs",
                    BeginLine = 5,
                    EndLine = 5,
                    Description = "The class Bar has too many fields.",
                    Class = "Bar",
                    Package = "MyApp",
                    RuleSetName = "codesize",
                    Priority = 3,
                },
                new Violation
                {
                    Rule = rule3,
                    File = "/src/Bar.cs",
                    BeginLine = 20,
                    EndLine = 20,
                    Description = "Avoid variables with short names like $x.",
                    Class = "Bar",
                    Method = "DoSomething",
                    Package = "MyApp",
                    RuleSetName = "naming",
                    Priority = 4,
                },
            },
            Errors = new List<ProcessingError>
            {
                new ProcessingError { File = "/src/Bad.cs", Message = "Syntax error on line 1." },
            },
        };
    }

    private static string Render(IRenderer r) =>
        Render(r, MakeReport());

    private static string Render(IRenderer r, ViolationReport report)
    {
        using var sw = new StringWriter();
        r.Render(sw, report);
        return sw.ToString();
    }

    // -------------------------------------------------------------------------
    // Text renderer
    // -------------------------------------------------------------------------

    [Fact]
    public void Text_ContainsFileAndRuleName()
    {
        var out_ = Render(new TextRenderer());
        Assert.Contains("/src/Foo.cs:10", out_);
        Assert.Contains("CyclomaticComplexity", out_);
        Assert.Contains("/src/Bar.cs:5", out_);
        Assert.Contains("TooManyFields", out_);
        Assert.Contains("/src/Bad.cs", out_);
        Assert.Contains("Syntax error on line 1.", out_);
    }

    // -------------------------------------------------------------------------
    // ANSI renderer
    // -------------------------------------------------------------------------

    [Fact]
    public void Ansi_ContainsEscapeCodesAndContent()
    {
        var out_ = Render(new AnsiRenderer());
        // Should contain ANSI escape codes
        Assert.Contains("\x1b[33m", out_);  // yellow for rule name
        Assert.Contains("\x1b[31m", out_);  // red for description
        Assert.Contains("\x1b[0m", out_);   // reset
        // Content is still present
        Assert.Contains("CyclomaticComplexity", out_);
        Assert.Contains("/src/Foo.cs:10", out_);
    }

    [Fact]
    public void Ansi_ErrorsOutput()
    {
        var out_ = Render(new AnsiRenderer());
        Assert.Contains("/src/Bad.cs", out_);
        Assert.Contains("Syntax error on line 1.", out_);
    }

    // -------------------------------------------------------------------------
    // XML renderer
    // -------------------------------------------------------------------------

    [Fact]
    public void Xml_IsValidXml()
    {
        var out_ = Render(new XmlRenderer());
        // Should not throw
        var doc = new XmlDocument();
        doc.LoadXml(out_);
        Assert.Equal("pmd", doc.DocumentElement!.Name);
    }

    [Fact]
    public void Xml_RootAttributes()
    {
        var out_ = Render(new XmlRenderer());
        var doc = new XmlDocument();
        doc.LoadXml(out_);
        var root = doc.DocumentElement!;
        Assert.Equal("messharp", root.GetAttribute("tool"));
        Assert.Equal("0.2.2", root.GetAttribute("version"));
        Assert.NotEmpty(root.GetAttribute("timestamp"));
    }

    [Fact]
    public void Xml_FileAndViolationElements()
    {
        var out_ = Render(new XmlRenderer());
        var doc = new XmlDocument();
        doc.LoadXml(out_);
        var files = doc.SelectNodes("//file")!;
        Assert.Equal(2, files.Count);  // 2 distinct files

        var violations = doc.SelectNodes("//violation")!;
        Assert.Equal(3, violations.Count);

        // Check attributes on first violation
        var v1 = violations[0]!;
        Assert.Equal("10", ((XmlElement)v1).GetAttribute("beginline"));
        Assert.Equal("30", ((XmlElement)v1).GetAttribute("endline"));
        Assert.Equal("CyclomaticComplexity", ((XmlElement)v1).GetAttribute("rule"));
        Assert.Equal("codesize", ((XmlElement)v1).GetAttribute("ruleset"));
        Assert.Equal("2", ((XmlElement)v1).GetAttribute("priority"));
    }

    [Fact]
    public void Xml_ErrorElements()
    {
        var out_ = Render(new XmlRenderer());
        var doc = new XmlDocument();
        doc.LoadXml(out_);
        var errors = doc.SelectNodes("//error")!;
        Assert.Equal(1, errors.Count);
        var e = (XmlElement)errors[0]!;
        Assert.Equal("/src/Bad.cs", e.GetAttribute("filename"));
        Assert.Contains("Syntax error", e.GetAttribute("msg"));
    }

    [Fact]
    public void Xml_XmlEscapesSpecialChars()
    {
        var rule = new FakeRule { Name = "TestRule", SetName = "test", Priority = 3 };
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Rule = rule,
                    File = "/src/A.cs",
                    BeginLine = 1,
                    EndLine = 1,
                    Description = "Use & instead of <and>",
                    RuleSetName = "test",
                    Priority = 3,
                },
            },
        };
        var out_ = Render(new XmlRenderer(), report);
        Assert.Contains("&amp;", out_);
        Assert.Contains("&lt;", out_);
        Assert.Contains("&gt;", out_);
    }

    // -------------------------------------------------------------------------
    // JSON renderer
    // -------------------------------------------------------------------------

    [Fact]
    public void Json_IsValidJson()
    {
        var out_ = Render(new JsonRenderer());
        var doc = JsonDocument.Parse(out_);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void Json_TopLevelFields()
    {
        var out_ = Render(new JsonRenderer());
        var doc = JsonDocument.Parse(out_);
        var root = doc.RootElement;
        Assert.Equal("messharp", root.GetProperty("package").GetString());
        Assert.Equal("0.2.2", root.GetProperty("version").GetString());
        Assert.True(root.TryGetProperty("timestamp", out _));
    }

    [Fact]
    public void Json_FilesAndViolations()
    {
        var out_ = Render(new JsonRenderer());
        var doc = JsonDocument.Parse(out_);
        var files = doc.RootElement.GetProperty("files");
        Assert.Equal(JsonValueKind.Array, files.ValueKind);
        Assert.Equal(2, files.GetArrayLength());  // 2 distinct files

        // First file
        var f1 = files[0];
        Assert.Equal("/src/Foo.cs", f1.GetProperty("file").GetString());
        var viols = f1.GetProperty("violations");
        Assert.Equal(1, viols.GetArrayLength());

        var v1 = viols[0];
        Assert.Equal(10, v1.GetProperty("beginLine").GetInt32());
        Assert.Equal(30, v1.GetProperty("endLine").GetInt32());
        Assert.Equal("CyclomaticComplexity", v1.GetProperty("rule").GetString());
        Assert.Equal("codesize", v1.GetProperty("ruleSet").GetString());
        Assert.Equal(2, v1.GetProperty("priority").GetInt32());
    }

    [Fact]
    public void Json_Errors()
    {
        var out_ = Render(new JsonRenderer());
        var doc = JsonDocument.Parse(out_);
        var errors = doc.RootElement.GetProperty("errors");
        Assert.Equal(1, errors.GetArrayLength());
        var e = errors[0];
        Assert.Equal("/src/Bad.cs", e.GetProperty("fileName").GetString());
        Assert.Contains("Syntax error", e.GetProperty("message").GetString());
    }

    [Fact]
    public void Json_NoErrors_OmitsErrorsKey()
    {
        var report = new ViolationReport
        {
            Violations = new List<Violation>(),
            Errors = new List<ProcessingError>(),
        };
        var out_ = Render(new JsonRenderer(), report);
        var doc = JsonDocument.Parse(out_);
        Assert.False(doc.RootElement.TryGetProperty("errors", out _));
    }

    // -------------------------------------------------------------------------
    // HTML renderer
    // -------------------------------------------------------------------------

    [Fact]
    public void Html_ContainsDoctype()
    {
        var out_ = Render(new HtmlRenderer());
        Assert.StartsWith("<!DOCTYPE html>", out_);
    }

    [Fact]
    public void Html_ContainsTitle()
    {
        var out_ = Render(new HtmlRenderer());
        Assert.Contains("messharp report", out_);
    }

    [Fact]
    public void Html_ContainsFileH2AndTableRow()
    {
        var out_ = Render(new HtmlRenderer());
        Assert.Contains("<h2>/src/Foo.cs</h2>", out_);
        Assert.Contains("<h2>/src/Bar.cs</h2>", out_);
        Assert.Contains("<td>10</td>", out_);
        Assert.Contains("CyclomaticComplexity", out_);
        Assert.Contains("<table border=\"1\"", out_);
        Assert.Contains("<th>Line</th>", out_);
    }

    [Fact]
    public void Html_EscapesEntities()
    {
        var rule = new FakeRule { Name = "R", SetName = "s", Priority = 3 };
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Rule = rule,
                    File = "/a.cs",
                    BeginLine = 1,
                    EndLine = 1,
                    Description = "a < b & c > d",
                    RuleSetName = "s",
                    Priority = 3,
                },
            },
        };
        var out_ = Render(new HtmlRenderer(), report);
        Assert.Contains("&lt;", out_);
        Assert.Contains("&amp;", out_);
        Assert.Contains("&gt;", out_);
    }

    // -------------------------------------------------------------------------
    // GitHub renderer
    // -------------------------------------------------------------------------

    [Fact]
    public void GitHub_ViolationLines()
    {
        var out_ = Render(new GitHubRenderer());
        var lines = out_.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);  // 3 violations + 1 error

        Assert.StartsWith("::warning ", lines[0]);
        Assert.Contains("file=/src/Foo.cs", lines[0]);
        Assert.Contains("line=10", lines[0]);
        Assert.Contains("CyclomaticComplexity", lines[0]);
    }

    [Fact]
    public void GitHub_ErrorLines()
    {
        var out_ = Render(new GitHubRenderer());
        Assert.Contains("::error file=/src/Bad.cs::Syntax error on line 1.", out_);
    }

    [Fact]
    public void GitHub_Format()
    {
        var out_ = Render(new GitHubRenderer());
        // Exact format: ::warning file=<path>,line=<n>,col=1::<desc> (<rule>)
        Assert.Contains("::warning file=/src/Foo.cs,line=10,col=1::", out_);
    }

    // -------------------------------------------------------------------------
    // GitLab renderer
    // -------------------------------------------------------------------------

    [Fact]
    public void GitLab_IsValidJsonArray()
    {
        var out_ = Render(new GitLabRenderer());
        var doc = JsonDocument.Parse(out_);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public void GitLab_EntryCount()
    {
        var out_ = Render(new GitLabRenderer());
        var doc = JsonDocument.Parse(out_);
        Assert.Equal(3, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void GitLab_RequiredKeys()
    {
        var out_ = Render(new GitLabRenderer());
        var doc = JsonDocument.Parse(out_);
        var entry = doc.RootElement[0];
        Assert.True(entry.TryGetProperty("type", out _));
        Assert.True(entry.TryGetProperty("check_name", out _));
        Assert.True(entry.TryGetProperty("description", out _));
        Assert.True(entry.TryGetProperty("fingerprint", out _));
        Assert.True(entry.TryGetProperty("severity", out _));
        Assert.True(entry.TryGetProperty("location", out _));
    }

    [Fact]
    public void GitLab_LocationStructure()
    {
        var out_ = Render(new GitLabRenderer());
        var doc = JsonDocument.Parse(out_);
        var entry = doc.RootElement[0];
        var loc = entry.GetProperty("location");
        Assert.Equal("/src/Foo.cs", loc.GetProperty("path").GetString());
        Assert.Equal(10, loc.GetProperty("lines").GetProperty("begin").GetInt32());
    }

    [Fact]
    public void GitLab_SeverityMapping()
    {
        var out_ = Render(new GitLabRenderer());
        var doc = JsonDocument.Parse(out_);
        // priority 2 => critical
        Assert.Equal("critical", doc.RootElement[0].GetProperty("severity").GetString());
        // priority 3 => major
        Assert.Equal("major", doc.RootElement[1].GetProperty("severity").GetString());
        // priority 4 => minor
        Assert.Equal("minor", doc.RootElement[2].GetProperty("severity").GetString());
    }

    [Fact]
    public void GitLab_FingerprintIsHexEncodedBytes()
    {
        var out_ = Render(new GitLabRenderer());
        var doc = JsonDocument.Parse(out_);
        var fp = doc.RootElement[0].GetProperty("fingerprint").GetString()!;
        // Must be hex (only 0-9 a-f)
        Assert.Matches("^[0-9a-f]+$", fp);
        // Decode and verify it's "file:line:ruleName"
        var bytes = Convert.FromHexString(fp);
        var decoded = Encoding.UTF8.GetString(bytes);
        Assert.Equal("/src/Foo.cs:10:CyclomaticComplexity", decoded);
    }

    [Fact]
    public void GitLab_FingerprintsAreUnique()
    {
        var out_ = Render(new GitLabRenderer());
        var doc = JsonDocument.Parse(out_);
        var fps = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("fingerprint").GetString())
            .ToList();
        Assert.Equal(fps.Count, fps.Distinct().Count());
    }

    // -------------------------------------------------------------------------
    // Checkstyle renderer
    // -------------------------------------------------------------------------

    [Fact]
    public void Checkstyle_IsValidXml()
    {
        var out_ = Render(new CheckstyleRenderer());
        var doc = new XmlDocument();
        doc.LoadXml(out_);
        Assert.Equal("checkstyle", doc.DocumentElement!.Name);
    }

    [Fact]
    public void Checkstyle_VersionAttribute()
    {
        var out_ = Render(new CheckstyleRenderer());
        var doc = new XmlDocument();
        doc.LoadXml(out_);
        Assert.Equal("0.2.2", doc.DocumentElement!.GetAttribute("version"));
    }

    [Fact]
    public void Checkstyle_FileAndErrorElements()
    {
        var out_ = Render(new CheckstyleRenderer());
        var doc = new XmlDocument();
        doc.LoadXml(out_);

        var files = doc.SelectNodes("//file")!;
        Assert.Equal(2, files.Count);

        var errors = doc.SelectNodes("//error")!;
        Assert.Equal(3, errors.Count);  // 3 violations become 3 <error> elements

        var e1 = (XmlElement)errors[0]!;
        Assert.Equal("10", e1.GetAttribute("line"));
        Assert.Equal("1", e1.GetAttribute("column"));
        Assert.Equal("error", e1.GetAttribute("severity"));  // priority 2 => error
        Assert.Contains("CyclomaticComplexity", e1.GetAttribute("source"));
    }

    [Fact]
    public void Checkstyle_SeverityMapping()
    {
        var out_ = Render(new CheckstyleRenderer());
        var doc = new XmlDocument();
        doc.LoadXml(out_);
        var errors = doc.SelectNodes("//error")!;

        // priority 2 => error
        Assert.Equal("error", ((XmlElement)errors[0]!).GetAttribute("severity"));
        // priority 3 => warning
        Assert.Equal("warning", ((XmlElement)errors[1]!).GetAttribute("severity"));
        // priority 4 => info
        Assert.Equal("info", ((XmlElement)errors[2]!).GetAttribute("severity"));
    }

    [Fact]
    public void Checkstyle_SourceContainsRuleSetAndName()
    {
        var out_ = Render(new CheckstyleRenderer());
        Assert.Contains("codesize/CyclomaticComplexity", out_);
    }

    // -------------------------------------------------------------------------
    // SARIF renderer
    // -------------------------------------------------------------------------

    [Fact]
    public void Sarif_IsValidJson()
    {
        var out_ = Render(new SarifRenderer());
        var doc = JsonDocument.Parse(out_);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void Sarif_RequiredTopLevelKeys()
    {
        var out_ = Render(new SarifRenderer());
        var doc = JsonDocument.Parse(out_);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("$schema", out _));
        Assert.True(root.TryGetProperty("version", out _));
        Assert.True(root.TryGetProperty("runs", out _));
    }

    [Fact]
    public void Sarif_Version()
    {
        var out_ = Render(new SarifRenderer());
        var doc = JsonDocument.Parse(out_);
        Assert.Equal("2.1.0", doc.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void Sarif_Schema()
    {
        var out_ = Render(new SarifRenderer());
        var doc = JsonDocument.Parse(out_);
        var schema = doc.RootElement.GetProperty("$schema").GetString();
        Assert.Contains("sarif-schema-2.1.0", schema);
    }

    [Fact]
    public void Sarif_ToolDriver()
    {
        var out_ = Render(new SarifRenderer());
        var doc = JsonDocument.Parse(out_);
        var driver = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("tool")
            .GetProperty("driver");
        Assert.Equal("messharp", driver.GetProperty("name").GetString());
        Assert.Equal("0.2.2", driver.GetProperty("version").GetString());
    }

    [Fact]
    public void Sarif_RulesDeduplication()
    {
        // 3 violations but only 3 distinct rules (all different in our fixture)
        var out_ = Render(new SarifRenderer());
        var doc = JsonDocument.Parse(out_);
        var rules = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("tool")
            .GetProperty("driver")
            .GetProperty("rules");
        Assert.Equal(3, rules.GetArrayLength());

        // If we add a duplicate violation, rules should still deduplicate
        var rule = new FakeRule { Name = "CyclomaticComplexity", SetName = "codesize", Priority = 2 };
        var report = MakeReport();
        report.Violations.Add(new Violation
        {
            Rule = rule,
            File = "/src/Foo.cs",
            BeginLine = 50,
            EndLine = 60,
            Description = "Again CC.",
            RuleSetName = "codesize",
            Priority = 2,
        });
        var out2 = Render(new SarifRenderer(), report);
        var doc2 = JsonDocument.Parse(out2);
        var rules2 = doc2.RootElement
            .GetProperty("runs")[0]
            .GetProperty("tool")
            .GetProperty("driver")
            .GetProperty("rules");
        Assert.Equal(3, rules2.GetArrayLength());  // still 3 unique rules
    }

    [Fact]
    public void Sarif_ResultStructure()
    {
        var out_ = Render(new SarifRenderer());
        var doc = JsonDocument.Parse(out_);
        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");
        Assert.Equal(3, results.GetArrayLength());

        var r1 = results[0];
        Assert.Equal("CyclomaticComplexity", r1.GetProperty("ruleId").GetString());
        Assert.Equal("error", r1.GetProperty("level").GetString());  // priority 2 => error
        Assert.True(r1.TryGetProperty("message", out _));
        Assert.True(r1.TryGetProperty("locations", out _));

        var physLoc = r1.GetProperty("locations")[0]
            .GetProperty("physicalLocation");
        Assert.Equal("/src/Foo.cs", physLoc.GetProperty("artifactLocation").GetProperty("uri").GetString());
        Assert.Equal(10, physLoc.GetProperty("region").GetProperty("startLine").GetInt32());
        Assert.Equal(30, physLoc.GetProperty("region").GetProperty("endLine").GetInt32());
    }

    [Fact]
    public void Sarif_LevelMapping()
    {
        var out_ = Render(new SarifRenderer());
        var doc = JsonDocument.Parse(out_);
        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");
        // priority 2 => error
        Assert.Equal("error", results[0].GetProperty("level").GetString());
        // priority 3 => warning
        Assert.Equal("warning", results[1].GetProperty("level").GetString());
        // priority 4 => warning
        Assert.Equal("warning", results[2].GetProperty("level").GetString());
    }

    // -------------------------------------------------------------------------
    // Renderers.TryGet wiring
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("xml")]
    [InlineData("json")]
    [InlineData("html")]
    [InlineData("ansi")]
    [InlineData("github")]
    [InlineData("gitlab")]
    [InlineData("checkstyle")]
    [InlineData("sarif")]
    [InlineData("text")]
    public void Renderers_TryGet_AllFormatsResolved(string format)
    {
        Assert.True(Renderers.TryGet(format, out var renderer));
        Assert.NotNull(renderer);
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("json")]
    [InlineData("html")]
    [InlineData("ansi")]
    [InlineData("github")]
    [InlineData("gitlab")]
    [InlineData("checkstyle")]
    [InlineData("sarif")]
    [InlineData("text")]
    public void Renderers_AllFormats_DoNotThrowOnEmptyReport(string format)
    {
        Assert.True(Renderers.TryGet(format, out var renderer));
        var empty = new ViolationReport();
        var ex = Record.Exception(() => Render(renderer!, empty));
        Assert.Null(ex);
    }

    // -------------------------------------------------------------------------
    // Helper: minimal IRule implementation for tests
    // -------------------------------------------------------------------------

    private sealed class FakeRule : IRule
    {
        public string Name { get; set; } = "FakeRule";
        public string Message { get; set; } = "";
        public int Priority { get; set; } = 3;
        public string SetName { get; set; } = "";
        public string ExternalUrl { get; set; } = "";
        public string Description { get; set; } = "";
        public string Since { get; set; } = "";
    }
}
