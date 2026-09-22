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
        Assert.Equal(BuildInfo.Version, root.GetAttribute("version"));
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

    [Fact]
    public void Xml_StripsControlCharactersFromFileName()
    {
        var rule = new FakeRule { Name = "R", SetName = "s", Priority = 3 };
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Rule = rule,
                    File = "/src/bad\x01.cs",
                    BeginLine = 1,
                    EndLine = 1,
                    Description = "problem",
                    RuleSetName = "s",
                    Priority = 3,
                },
            },
            Errors = new List<ProcessingError>
            {
                new ProcessingError { File = "/src/bad\x1F.cs", Message = "msg \x0B with control" },
            },
        };
        var out_ = Render(new XmlRenderer(), report);

        var doc = new XmlDocument();
        doc.LoadXml(out_);  // throws on XML 1.0-illegal characters

        // Ordinal: culture-aware IndexOf treats control characters as ignorable.
        Assert.True(out_.IndexOf('\u0001') < 0, "U+0001 must be stripped");
        Assert.True(out_.IndexOf('\u001F') < 0, "U+001F must be stripped");
        Assert.True(out_.IndexOf('\u000B') < 0, "U+000B must be stripped");
        Assert.Contains("bad.cs", out_);
    }

    [Fact]
    public void Xml_XmlEscape_PreservesLegalWhitespaceAndUnicode()
    {
        var rule = new FakeRule { Name = "R", SetName = "s", Priority = 3 };
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Rule = rule,
                    File = "a\tb\nc\rd é\u00E9.cs",
                    BeginLine = 1,
                    EndLine = 1,
                    Description = "'quoted'?",
                    RuleSetName = "s",
                    Priority = 3,
                },
            },
        };
        var out_ = Render(new XmlRenderer(), report);

        var doc = new XmlDocument();
        doc.LoadXml(out_);
        Assert.Contains("&#039;quoted&#039;?", out_);
        Assert.Contains("a\tb\nc\rd é\u00E9.cs", out_);
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
        Assert.Equal(BuildInfo.Version, root.GetProperty("version").GetString());
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

    [Fact]
    public void Html_StripsControlCharacters()
    {
        var rule = new FakeRule { Name = "R", SetName = "s", Priority = 3 };
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Rule = rule,
                    File = "/src/bad\x01.cs",
                    BeginLine = 1,
                    EndLine = 1,
                    Description = "problem \x0C text",
                    RuleSetName = "s",
                    Priority = 3,
                },
            },
        };
        var out_ = Render(new HtmlRenderer(), report);

        // Ordinal: culture-aware IndexOf treats control characters as ignorable.
        Assert.True(out_.IndexOf('\u0001') < 0, "U+0001 must be stripped");
        Assert.True(out_.IndexOf('\u000C') < 0, "U+000C must be stripped");
        Assert.Contains("bad.cs", out_);
        Assert.Contains("problem  text", out_);
    }

    [Fact]
    public void Html_Errors()
    {
        var out_ = Render(new HtmlRenderer());
        Assert.Contains("<p>/src/Bad.cs: Syntax error on line 1.</p>", out_);
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

    [Fact]
    public void GitHub_EscapesWorkflowCommandProperties()
    {
        var rule = new FakeRule { Name = "R", SetName = "s", Priority = 3 };
        var file = "src/with%percent:colon,comma\r\nfile.cs";
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Rule = rule,
                    File = file,
                    BeginLine = 7,
                    EndLine = 7,
                    Description = "problem",
                    RuleSetName = "s",
                    Priority = 3,
                },
            },
            Errors = new List<ProcessingError>
            {
                new ProcessingError { File = file, Message = "parse error" },
            },
        };

        var out_ = Render(new GitHubRenderer(), report);

        Assert.Contains("::warning file=src/with%25percent%3Acolon%2Ccomma%0D%0Afile.cs,line=7,col=1::", out_);
        Assert.Contains("::error file=src/with%25percent%3Acolon%2Ccomma%0D%0Afile.cs::parse error", out_);
    }

    [Fact]
    public void GitHub_EscapesWorkflowCommandData()
    {
        var rule = new FakeRule { Name = "Rule%Name\r\n", SetName = "s", Priority = 3 };
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Rule = rule,
                    File = "src/file.cs",
                    BeginLine = 7,
                    EndLine = 7,
                    Description = "description%value\r\n::error file=evil.cs::injected",
                    RuleSetName = "s",
                    Priority = 3,
                },
            },
            Errors = new List<ProcessingError>
            {
                new ProcessingError
                {
                    File = "src/bad.cs",
                    Message = "error%message\r\n::error file=evil.cs::injected",
                },
            },
        };

        var out_ = Render(new GitHubRenderer(), report);

        Assert.Contains(
            "::warning file=src/file.cs,line=7,col=1::description%25value%0D%0A::error file=evil.cs::injected (Rule%25Name%0D%0A)",
            out_);
        Assert.Contains(
            "::error file=src/bad.cs::error%25message%0D%0A::error file=evil.cs::injected",
            out_);
        var lines = out_.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal(1, lines.Count(line => line.StartsWith("::warning ", StringComparison.Ordinal)));
        Assert.Equal(1, lines.Count(line => line.StartsWith("::error ", StringComparison.Ordinal)));
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
        Assert.Equal(4, doc.RootElement.GetArrayLength());
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

    [Fact]
    public void GitLab_ErrorEntries()
    {
        var out_ = Render(new GitLabRenderer());
        var doc = JsonDocument.Parse(out_);
        var entry = doc.RootElement[3];
        Assert.Equal("issue", entry.GetProperty("type").GetString());
        Assert.Equal("parse-error", entry.GetProperty("check_name").GetString());
        Assert.Equal("Syntax error on line 1.", entry.GetProperty("description").GetString());
        Assert.Equal("blocker", entry.GetProperty("severity").GetString());
        Assert.Equal("/src/Bad.cs", entry.GetProperty("location").GetProperty("path").GetString());

        var fp = entry.GetProperty("fingerprint").GetString()!;
        var decoded = Encoding.UTF8.GetString(Convert.FromHexString(fp));
        Assert.Equal("/src/Bad.cs:Syntax error on line 1.", decoded);
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
        Assert.Equal(BuildInfo.Version, doc.DocumentElement!.GetAttribute("version"));
    }

    [Fact]
    public void Checkstyle_FileAndErrorElements()
    {
        var out_ = Render(new CheckstyleRenderer());
        var doc = new XmlDocument();
        doc.LoadXml(out_);

        var files = doc.SelectNodes("//file")!;
        Assert.Equal(3, files.Count);

        var errors = doc.SelectNodes("//error")!;
        Assert.Equal(4, errors.Count);  // 3 violations + 1 processing error

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

    [Fact]
    public void Checkstyle_ProcessingErrors()
    {
        var out_ = Render(new CheckstyleRenderer());
        var doc = new XmlDocument();
        doc.LoadXml(out_);
        var files = doc.SelectNodes("//file")!;
        var last = (XmlElement)files[files.Count - 1]!;
        Assert.Equal("/src/Bad.cs", last.GetAttribute("name"));
        var error = (XmlElement)last.SelectSingleNode("error")!;
        Assert.Equal("0", error.GetAttribute("line"));
        Assert.Equal("1", error.GetAttribute("column"));
        Assert.Equal("error", error.GetAttribute("severity"));
        Assert.Equal("Syntax error on line 1.", error.GetAttribute("message"));
        Assert.Equal("messharp/parse-error", error.GetAttribute("source"));
    }

    [Fact]
    public void Checkstyle_StripsControlCharactersFromFileName()
    {
        var rule = new FakeRule { Name = "R", SetName = "s", Priority = 3 };
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Rule = rule,
                    File = "/src/bad\x01.cs",
                    BeginLine = 1,
                    EndLine = 1,
                    Description = "problem",
                    RuleSetName = "s",
                    Priority = 3,
                },
            },
        };
        var out_ = Render(new CheckstyleRenderer(), report);

        var doc = new XmlDocument();
        doc.LoadXml(out_);  // throws on XML 1.0-illegal characters

        // Ordinal: culture-aware IndexOf treats control characters as ignorable.
        Assert.True(out_.IndexOf('\u0001') < 0, "U+0001 must be stripped");
        Assert.Contains("bad.cs", out_);
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
        Assert.Equal(BuildInfo.Version, driver.GetProperty("version").GetString());
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
        Assert.Equal(4, results.GetArrayLength());

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

    [Fact]
    public void Sarif_ErrorResults()
    {
        var out_ = Render(new SarifRenderer());
        var doc = JsonDocument.Parse(out_);
        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");
        var err = results[3];
        Assert.Equal("error", err.GetProperty("level").GetString());
        Assert.Equal("Syntax error on line 1.", err.GetProperty("message").GetProperty("text").GetString());
        var physLoc = err.GetProperty("locations")[0].GetProperty("physicalLocation");
        Assert.Equal("/src/Bad.cs", physLoc.GetProperty("artifactLocation").GetProperty("uri").GetString());
        Assert.False(physLoc.TryGetProperty("region", out _));
    }

    [Fact]
    public void Sarif_ArtifactLocationUri_PercentEncodesSpecialCharactersAndNormalizesSeparators()
    {
        var rule = new FakeRule { Name = "CyclomaticComplexity", SetName = "codesize", Priority = 2 };
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new Violation
                {
                    Rule = rule,
                    File = "docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/ci/My Project & Co/Café Box.cs",
                    BeginLine = 5,
                    EndLine = 5,
                    Description = "Method complexity is high.",
                    RuleSetName = "codesize",
                    Priority = 2,
                },
            },
            Errors = new List<ProcessingError>
            {
                new ProcessingError
                {
                    File = @"windows\path\with spaces and #hash\Café error.cs",
                    Message = "Syntax error on line 1.",
                },
            },
        };

        var out_ = Render(new SarifRenderer(), report);
        var doc = JsonDocument.Parse(out_);
        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");

        var violationUri = results[0].GetProperty("locations")[0]
            .GetProperty("physicalLocation")
            .GetProperty("artifactLocation")
            .GetProperty("uri").GetString();
        Assert.Equal(
            "docs/exploratory-testing/2026-09-19-modern-csharp/fixtures/ci/My%20Project%20%26%20Co/Caf%C3%A9%20Box.cs",
            violationUri);

        var errorUri = results[1].GetProperty("locations")[0]
            .GetProperty("physicalLocation")
            .GetProperty("artifactLocation")
            .GetProperty("uri").GetString();
        Assert.Equal(
            "windows/path/with%20spaces%20and%20%23hash/Caf%C3%A9%20error.cs",
            errorUri);
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
    [InlineData("html")]
    [InlineData("checkstyle")]
    [InlineData("gitlab")]
    [InlineData("sarif")]
    public void Renderer_SurfacesProcessingErrors(string format)
    {
        var report = new ViolationReport
        {
            Errors = new List<ProcessingError>
            {
                new ProcessingError { File = "broken.cs", Message = "Syntax error" },
            },
        };
        Assert.True(Renderers.TryGet(format, out var renderer));
        var output = Render(renderer!, report);
        Assert.Contains("broken.cs", output);
        Assert.Contains("Syntax error", output);
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
