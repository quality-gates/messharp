using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    [Theory]
    [InlineData("xml")]
    [InlineData("checkstyle")]
    public void InterleavedReport_GroupsRecordsByFile(string format)
    {
        var rule = new FakeRule { Name = "Rule", SetName = "test", Priority = 3 };
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new() { Rule = rule, File = "/src/A.cs", BeginLine = 20, Description = "A twenty", RuleSetName = "test", Priority = 3 },
                new() { Rule = rule, File = "/src/B.cs", BeginLine = 5, Description = "B five", RuleSetName = "test", Priority = 3 },
                new() { Rule = rule, File = "/src/A.cs", BeginLine = 10, Description = "A ten", RuleSetName = "test", Priority = 3 },
            },
            Errors = new List<ProcessingError>
            {
                new() { File = "/src/A.cs", Message = "A error one" },
                new() { File = "/src/C.cs", Message = "C error" },
                new() { File = "/src/A.cs", Message = "A error two" },
            },
        };
        Assert.True(Renderers.TryGet(format, out var renderer));
        var doc = new XmlDocument();
        doc.LoadXml(Render(renderer, report));

        var actualViolations = doc.SelectNodes(format == "xml" ? "/pmd/file/violation" : "/checkstyle/file/error[@source='test/Rule']")!
            .Cast<XmlElement>()
            .Select(e => ($"{((XmlElement)e.ParentNode!).GetAttribute("name")}:{e.GetAttribute(format == "xml" ? "beginline" : "line")}",
                format == "xml" ? e.InnerText.Trim() : e.GetAttribute("message")))
            .OrderBy(x => x.Item1, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { ("/src/A.cs:10", "A ten"), ("/src/A.cs:20", "A twenty"), ("/src/B.cs:5", "B five") }, actualViolations);

        var actualErrors = doc.SelectNodes(format == "xml" ? "/pmd/error" : "/checkstyle/file/error[@source='messharp/parse-error']")!
            .Cast<XmlElement>()
            .Select(e => (format == "xml" ? e.GetAttribute("filename") : ((XmlElement)e.ParentNode!).GetAttribute("name"),
                e.GetAttribute(format == "xml" ? "msg" : "message")))
            .OrderBy(x => x.Item1, StringComparer.Ordinal).ThenBy(x => x.Item2, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { ("/src/A.cs", "A error one"), ("/src/A.cs", "A error two"), ("/src/C.cs", "C error") }, actualErrors);

        var paths = doc.SelectNodes(format == "xml" ? "/pmd/file" : "/checkstyle/file")!
            .Cast<XmlElement>().Select(e => e.GetAttribute("name")).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(format == "xml" ? new[] { "/src/A.cs", "/src/B.cs" } : new[] { "/src/A.cs", "/src/B.cs", "/src/C.cs" }, paths);
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("checkstyle")]
    public void SortedSingleFileReport_HasOneFileSection(string format)
    {
        var report = MakeReport();
        report.Violations.RemoveAll(v => v.File != "/src/Bar.cs");
        report.Errors.Clear();
        Assert.True(Renderers.TryGet(format, out var renderer));
        var doc = new XmlDocument();
        doc.LoadXml(Render(renderer, report));
        var file = Assert.Single(doc.SelectNodes(format == "xml" ? "/pmd/file" : "/checkstyle/file")!.Cast<XmlElement>());
        Assert.Equal("/src/Bar.cs", file.GetAttribute("name"));
        Assert.Equal(2, file.ChildNodes.Count);
    }

    public static IEnumerable<object[]> RegisteredFormats => Renderers.Formats.Select(format => new object[] { format });

    private sealed record RenderedRecord(string Kind, string Path, string Message);

    [Theory]
    [MemberData(nameof(RegisteredFormats))]
    public void RegisteredFormat_PreservesEscapedPathsAndDuplicateRecordsAcrossMutation(string format)
    {
        const string escapedPath = "nested/odd path & <tag> #café,25%.cs";
        var rule = new FakeRule { Name = "Rule", SetName = "test", Priority = 3 };
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new() { Rule = rule, File = escapedPath, BeginLine = 8, Description = "duplicate finding", RuleSetName = "test" },
                new() { Rule = rule, File = "src/other.cs", BeginLine = 3, Description = "single finding", RuleSetName = "test" },
                new() { Rule = rule, File = escapedPath, BeginLine = 8, Description = "duplicate finding", RuleSetName = "test" },
            },
            Errors = new List<ProcessingError>
            {
                new() { File = escapedPath, Message = "duplicate parse error" },
                new() { File = "error-only.cs", Message = "single parse error" },
                new() { File = escapedPath, Message = "duplicate parse error" },
            },
        };
        var expected = new[]
        {
            new RenderedRecord("violation", RenderedPath(format, escapedPath), "duplicate finding"),
            new RenderedRecord("violation", RenderedPath(format, "src/other.cs"), "single finding"),
            new RenderedRecord("violation", RenderedPath(format, escapedPath), "duplicate finding"),
            new RenderedRecord("error", RenderedPath(format, escapedPath), "duplicate parse error"),
            new RenderedRecord("error", RenderedPath(format, "error-only.cs"), "single parse error"),
            new RenderedRecord("error", RenderedPath(format, escapedPath), "duplicate parse error"),
        };
        Assert.True(Renderers.TryGet(format, out var renderer));
        AssertRecords(format, Render(renderer, report), expected);

        report.Violations.Clear();
        report.Errors.Clear();
        report.Errors.Add(new ProcessingError { File = "new.cs", Message = "new-error" });
        AssertRecords(format, Render(renderer, report), new[]
        {
            new RenderedRecord("error", "new.cs", "new-error"),
        });
    }

    private static string RenderedPath(string format, string path) => format switch
    {
        "github" => path.Replace("%", "%25", StringComparison.Ordinal).Replace(",", "%2C", StringComparison.Ordinal),
        "sarif" when path == "nested/odd path & <tag> #café,25%.cs" => "nested/odd%20path%20%26%20%3Ctag%3E%20%23caf%C3%A9%2C25%25.cs",
        _ => path,
    };

    private static void AssertRecords(string format, string output, IEnumerable<RenderedRecord> expected)
    {
        var expectedRecords = expected
            .OrderBy(record => record.Kind, StringComparer.Ordinal)
            .ThenBy(record => record.Path, StringComparer.Ordinal)
            .ThenBy(record => record.Message, StringComparer.Ordinal)
            .ToArray();
        var actualRecords = ReadRecords(format, output, expectedRecords.Select(record => record.Message).Distinct().ToArray())
            .OrderBy(record => record.Kind, StringComparer.Ordinal)
            .ThenBy(record => record.Path, StringComparer.Ordinal)
            .ThenBy(record => record.Message, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedRecords, actualRecords);
    }

    private static RenderedRecord[] ReadRecords(string format, string output, string[] messages)
    {
        string FindMessage(string value) => messages.Single(message => value.Contains(message, StringComparison.Ordinal));

        if (format is "text" or "ansi")
        {
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
            {
                var errorSeparator = line.IndexOf("\t-\t", StringComparison.Ordinal);
                if (errorSeparator >= 0)
                    return new RenderedRecord("error", line[..errorSeparator], line[(errorSeparator + 3)..].TrimEnd('\r'));
                var pathSeparator = line.IndexOf(':');
                return new RenderedRecord("violation", line[..pathSeparator], FindMessage(line));
            }).ToArray();
        }

        if (format == "github")
        {
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
            {
                var fileStart = line.IndexOf(" file=", StringComparison.Ordinal) + " file=".Length;
                if (line.StartsWith("::error ", StringComparison.Ordinal))
                {
                    var dataStart = line.IndexOf("::", fileStart, StringComparison.Ordinal);
                    return new RenderedRecord("error", line[fileStart..dataStart], line[(dataStart + 2)..]);
                }
                var locationEnd = line.IndexOf(",line=", fileStart, StringComparison.Ordinal);
                var messageStart = line.IndexOf("::", locationEnd, StringComparison.Ordinal) + 2;
                return new RenderedRecord("violation", line[fileStart..locationEnd], FindMessage(line[messageStart..]));
            }).ToArray();
        }

        if (format == "gitlab")
        {
            using var doc = JsonDocument.Parse(output);
            return doc.RootElement.EnumerateArray().Select(entry => new RenderedRecord(
                entry.GetProperty("check_name").GetString() == "parse-error" ? "error" : "violation",
                entry.GetProperty("location").GetProperty("path").GetString()!,
                entry.GetProperty("description").GetString()!)).ToArray();
        }

        if (format == "sarif")
        {
            using var doc = JsonDocument.Parse(output);
            return doc.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().Select(entry => new RenderedRecord(
                entry.GetProperty("ruleId").GetString() == "" ? "error" : "violation",
                entry.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString()!,
                entry.GetProperty("message").GetProperty("text").GetString()!)).ToArray();
        }

        if (format == "json")
        {
            using var doc = JsonDocument.Parse(output);
            return doc.RootElement.GetProperty("files").EnumerateArray()
                .SelectMany(file => file.GetProperty("violations").EnumerateArray().Select(violation => new RenderedRecord(
                    "violation", file.GetProperty("file").GetString()!, violation.GetProperty("description").GetString()!)))
                .Concat(doc.RootElement.GetProperty("errors").EnumerateArray().Select(error => new RenderedRecord(
                    "error", error.GetProperty("fileName").GetString()!, error.GetProperty("message").GetString()!)))
                .ToArray();
        }

        if (format == "html")
        {
            var doc = new XmlDocument();
            doc.LoadXml(output.Replace("<!DOCTYPE html>", "", StringComparison.Ordinal).Replace("<meta charset=\"utf-8\">", "<meta charset=\"utf-8\"/>", StringComparison.Ordinal));
            return doc.SelectNodes("//table/tr[td]")!.Cast<XmlElement>()
                .Select(row => new RenderedRecord("violation", row.ParentNode!.PreviousSibling!.InnerText, row.ChildNodes[2]!.InnerText))
                .Concat(doc.SelectNodes("//p")!.Cast<XmlElement>().Select(error =>
                {
                    var parts = error.InnerText.Split(": ", 2);
                    return new RenderedRecord("error", parts[0], parts[1]);
                })).ToArray();
        }

        var xml = new XmlDocument();
        xml.LoadXml(output);
        if (format == "xml")
            return xml.SelectNodes("/pmd/file/violation")!.Cast<XmlElement>()
                .Select(violation => new RenderedRecord("violation", ((XmlElement)violation.ParentNode!).GetAttribute("name"), violation.InnerText.Trim()))
                .Concat(xml.SelectNodes("/pmd/error")!.Cast<XmlElement>().Select(error => new RenderedRecord(
                    "error", error.GetAttribute("filename"), error.GetAttribute("msg"))))
                .ToArray();

        if (format == "checkstyle")
            return xml.SelectNodes("/checkstyle/file/error")!.Cast<XmlElement>().Select(error => new RenderedRecord(
                error.GetAttribute("source") == "messharp/parse-error" ? "error" : "violation",
                ((XmlElement)error.ParentNode!).GetAttribute("name"),
                error.GetAttribute("message"))).ToArray();

        throw new InvalidOperationException($"No record reader for {format}");
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("checkstyle")]
    [InlineData("html")]
    [InlineData("json")]
    public void GroupedFormats_OrderAndEscapeRecordsAndKeepErrorChannels(string format)
    {
        var rule = new FakeRule { Name = "Rule", SetName = "test" };
        var report = new ViolationReport
        {
            Violations = new List<Violation>
            {
                new() { Rule = rule, File = "b&.cs", BeginLine = 9, Description = "late & <tag>", RuleSetName = "test" },
                new() { Rule = rule, File = "A.cs", BeginLine = 4, Description = "alpha", RuleSetName = "test" },
                new() { Rule = rule, File = "b&.cs", BeginLine = 2, Description = "early", RuleSetName = "test" },
            },
            Errors = new List<ProcessingError>
            {
                new() { File = "z.cs", Message = "z-error" },
                new() { File = "b&.cs", Message = "b & error" },
                new() { File = "z.cs", Message = "z-error" },
            },
        };
        Assert.True(Renderers.TryGet(format, out var renderer));
        var output = Render(renderer, report);
        if (format == "json")
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;
            var files = root.GetProperty("files");
            Assert.Equal(new[] { "A.cs", "b&.cs" }, files.EnumerateArray().Select(f => f.GetProperty("file").GetString()));
            Assert.Equal(new[] { 2, 9 }, files[1].GetProperty("violations").EnumerateArray().Select(v => v.GetProperty("beginLine").GetInt32()));
            Assert.Equal("late & <tag>", files[1].GetProperty("violations")[1].GetProperty("description").GetString());
            Assert.Equal(new[] { "z-error", "b & error", "z-error" }, root.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("message").GetString()));
        }
        else if (format == "html")
        {
            var doc = new XmlDocument();
            doc.LoadXml(output.Replace("<!DOCTYPE html>", "", StringComparison.Ordinal).Replace("<meta charset=\"utf-8\">", "<meta charset=\"utf-8\"/>", StringComparison.Ordinal));
            Assert.Equal(new[] { "A.cs", "b&.cs" }, doc.SelectNodes("//h2")!.Cast<XmlElement>().Select(e => e.InnerText));
            Assert.True(output.IndexOf("early", StringComparison.Ordinal) < output.IndexOf("late &amp; &lt;tag&gt;", StringComparison.Ordinal));
            Assert.Contains("<p>b&amp;.cs: b &amp; error</p>", output);
            Assert.True(output.IndexOf("z-error", StringComparison.Ordinal) < output.IndexOf("b &amp; error", StringComparison.Ordinal));
        }
        else
        {
            var doc = new XmlDocument();
            doc.LoadXml(output);
            var files = doc.SelectNodes(format == "xml" ? "/pmd/file" : "/checkstyle/file")!.Cast<XmlElement>().ToArray();
            Assert.Equal(format == "xml" ? new[] { "A.cs", "b&.cs" } : new[] { "A.cs", "b&.cs", "z.cs" }, files.Select(f => f.GetAttribute("name")));
            var violations = files[1].SelectNodes(format == "xml" ? "violation" : "error[@source='test/Rule']")!.Cast<XmlElement>().ToArray();
            Assert.Equal(new[] { "2", "9" }, violations.Select(v => v.GetAttribute(format == "xml" ? "beginline" : "line")));
            Assert.Equal("late & <tag>", format == "xml" ? violations[1].InnerText.Trim() : violations[1].GetAttribute("message"));
            var errors = doc.SelectNodes(format == "xml" ? "/pmd/error" : "/checkstyle/file/error[@source='messharp/parse-error']")!.Cast<XmlElement>().Select(e => e.GetAttribute(format == "xml" ? "msg" : "message"));
            Assert.Equal(format == "xml" ? new[] { "z-error", "b & error", "z-error" } : new[] { "b & error", "z-error", "z-error" }, errors);
        }
    }

    [Fact]
    public void Report_GroupsAreFreshAndSortedWithStableTies()
    {
        var rule = new FakeRule { Name = "Rule" };
        var report = new ViolationReport();
        report.Violations.Add(new Violation { Rule = rule, File = "b", BeginLine = 5, Description = "first" });
        report.Violations.Add(new Violation { Rule = rule, File = "", BeginLine = 1 });
        report.Violations.Add(new Violation { Rule = rule, File = "A", BeginLine = 2 });
        report.Violations.Add(new Violation { Rule = rule, File = "b", BeginLine = 1 });
        report.Violations.Add(new Violation { Rule = rule, File = "b", BeginLine = 5, Description = "second" });
        report.Errors.Add(new ProcessingError { File = "c", Message = "duplicate" });
        report.Errors.Add(new ProcessingError { File = "c", Message = "duplicate" });
        var groups = report.GetFiles();
        Assert.Equal(new[] { "", "A", "b", "c" }, groups.Select(g => g.Path));
        Assert.Equal(new[] { 1, 5, 5 }, groups[2].Violations.Select(v => v.BeginLine));
        Assert.Equal(new[] { "", "first", "second" }, groups[2].Violations.Select(v => v.Description));
        Assert.Equal(2, groups[3].Errors.Count);
        report.Violations.Clear();
        report.Errors.Clear();
        Assert.Empty(report.GetFiles());
        Assert.Equal(3, groups[2].Violations.Count);
    }

    [Theory]
    [InlineData("html", true)]
    [InlineData("html", false)]
    [InlineData("xml", true)]
    [InlineData("xml", false)]
    [InlineData("json", true)]
    [InlineData("json", false)]
    [InlineData("checkstyle", true)]
    [InlineData("checkstyle", false)]
    [InlineData("text", true)]
    [InlineData("text", false)]
    [InlineData("ansi", true)]
    [InlineData("ansi", false)]
    [InlineData("github", true)]
    [InlineData("github", false)]
    [InlineData("gitlab", true)]
    [InlineData("gitlab", false)]
    [InlineData("sarif", true)]
    [InlineData("sarif", false)]
    public void SortedReports_MatchBaselineBytes(string format, bool single)
    {
        var report = MakeReport();
        report.Errors.Clear();
        report.SortViolations();
        if (single) report.Violations.RemoveAll(v => v.File != "/src/Bar.cs");
        Assert.True(Renderers.TryGet(format, out var renderer));
        var file = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "RendererGoldens", $"{format}-{(single ? "single" : "multi")}.golden");
        var output = Render(renderer, report);
        if (format == "xml") output = Regex.Replace(output, "timestamp=\"[^\"]+\"", "timestamp=\"TIMESTAMP\"");
        if (format == "json") output = Regex.Replace(output, "\"timestamp\": \"[^\"]+\"", "\"timestamp\": \"TIMESTAMP\"");
        Assert.Equal(File.ReadAllBytes(file), Encoding.UTF8.GetBytes(output));
    }

    [Fact]
    public void Checkstyle_MergesErrorsIntoTheirFileAndOrdersFiles()
    {
        var report = MakeReport();
        report.SortViolations();
        report.Errors.Clear();
        report.Errors.Add(new ProcessingError { File = "/src/Foo.cs", Message = "first" });
        report.Errors.Add(new ProcessingError { File = "/src/Bad.cs", Message = "other" });
        report.Errors.Add(new ProcessingError { File = "/src/Foo.cs", Message = "second" });
        var doc = new XmlDocument();
        doc.LoadXml(Render(new CheckstyleRenderer(), report));
        var files = doc.SelectNodes("/checkstyle/file")!.Cast<XmlElement>().ToArray();
        Assert.Equal(new[] { "/src/Bad.cs", "/src/Bar.cs", "/src/Foo.cs" }, files.Select(f => f.GetAttribute("name")));
        Assert.Equal(new[] { "first", "second" }, files[2].SelectNodes("error[@source='messharp/parse-error']")!.Cast<XmlElement>().Select(e => e.GetAttribute("message")));
        Assert.Single(files[2].SelectNodes("error[@source='codesize/CyclomaticComplexity']")!.Cast<XmlElement>());
    }

    [Theory]
    [MemberData(nameof(RegisteredFormats))]
    public void NullProcessingErrorPath_KeepsTheErrorInEachFormatSchema(string format)
    {
        var report = new ViolationReport();
        report.Errors.Add(new ProcessingError { File = null!, Message = "missing path" });
        Assert.True(Renderers.TryGet(format, out var renderer));
        var output = Render(renderer, report);
        Assert.Equal(1, output.Split("missing path", StringSplitOptions.None).Length - 1);
        switch (format)
        {
            case "xml":
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(output);
                    var error = (XmlElement)doc.SelectSingleNode("/pmd/error")!;
                    Assert.Equal("", error.GetAttribute("filename"));
                    Assert.Equal("missing path", error.GetAttribute("msg"));
                    break;
                }
            case "checkstyle":
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(output);
                    var file = (XmlElement)doc.SelectSingleNode("/checkstyle/file")!;
                    Assert.Equal("", file.GetAttribute("name"));
                    Assert.Equal("messharp/parse-error", file.SelectSingleNode("error")!.Attributes!["source"]!.Value);
                    Assert.Equal("missing path", file.SelectSingleNode("error")!.Attributes!["message"]!.Value);
                    break;
                }
            case "html":
                Assert.Contains("<p>: missing path</p>", output);
                break;
            case "json":
                {
                    using var doc = JsonDocument.Parse(output);
                    var error = doc.RootElement.GetProperty("errors")[0];
                    Assert.False(error.TryGetProperty("fileName", out _));
                    Assert.Equal("missing path", error.GetProperty("message").GetString());
                    break;
                }
            case "text":
            case "ansi":
                Assert.Contains("\t-\tmissing path", output);
                break;
            case "github":
                Assert.Contains("::error file=::missing path", output);
                break;
            case "gitlab":
                {
                    using var doc = JsonDocument.Parse(output);
                    var error = doc.RootElement[0];
                    Assert.Equal("parse-error", error.GetProperty("check_name").GetString());
                    Assert.Equal(JsonValueKind.Null, error.GetProperty("location").GetProperty("path").ValueKind);
                    Assert.Equal("missing path", error.GetProperty("description").GetString());
                    break;
                }
            case "sarif":
                {
                    using var doc = JsonDocument.Parse(output);
                    var error = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
                    Assert.Equal("", error.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString());
                    Assert.Equal("missing path", error.GetProperty("message").GetProperty("text").GetString());
                    break;
                }
            default:
                throw new InvalidOperationException($"No null-path assertion for {format}");
        }
    }

    [Theory]
    [MemberData(nameof(RegisteredFormats))]
    public void RenderingAndMutatingProjection_DoNotMutateTheReport(string format)
    {
        var report = MakeReport();
        var violations = report.Violations.ToArray();
        var errors = report.Errors.ToArray();
        var expectedRecords = new[]
        {
            new RenderedRecord("violation", RenderedPath(format, "/src/Foo.cs"), "The method foo() has a Cyclomatic Complexity of 15."),
            new RenderedRecord("violation", RenderedPath(format, "/src/Bar.cs"), "The class Bar has too many fields."),
            new RenderedRecord("violation", RenderedPath(format, "/src/Bar.cs"), "Avoid variables with short names like $x."),
            new RenderedRecord("error", RenderedPath(format, "/src/Bad.cs"), "Syntax error on line 1."),
        };
        Assert.True(Renderers.TryGet(format, out var renderer));
        var before = Render(renderer, report);
        AssertRecords(format, before, expectedRecords);
        Assert.Equal(violations, report.Violations);
        Assert.Equal(errors, report.Errors);
        var projection = report.GetFiles();
        foreach (var file in projection) { file.Violations.Clear(); file.Errors.Clear(); }
        var after = Render(renderer, report);
        AssertRecords(format, after, expectedRecords);
        Assert.Equal(violations, report.Violations);
        Assert.Equal(errors, report.Errors);
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

        var v1 = doc.SelectSingleNode("/pmd/file[@name='/src/Foo.cs']/violation")!;
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

        var f1 = files.EnumerateArray().Single(f => f.GetProperty("file").GetString() == "/src/Foo.cs");
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

    [Fact]
    public void GitLab_ErrorEntryUsesDiagnosticLine()
    {
        var report = new ViolationReport
        {
            Errors = new List<ProcessingError>
            {
                new ProcessingError { File = "Broken.cs", Message = "CS1026: ) expected on line 4", Line = 4 },
            },
        };
        var doc = JsonDocument.Parse(Render(new GitLabRenderer(), report));
        var lines = doc.RootElement[0].GetProperty("location").GetProperty("lines");
        Assert.Equal(4, lines.GetProperty("begin").GetInt32());
    }

    [Fact]
    public void GitLab_ErrorEntryWithoutLinePointsAtFirstLine()
    {
        var out_ = Render(new GitLabRenderer());
        var doc = JsonDocument.Parse(out_);
        var lines = doc.RootElement[3].GetProperty("location").GetProperty("lines");
        Assert.Equal(1, lines.GetProperty("begin").GetInt32());
        Assert.DoesNotContain("\"begin\": 0", out_);
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

        var e1 = (XmlElement)doc.SelectSingleNode("/checkstyle/file[@name='/src/Foo.cs']/error")!;
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
        Assert.Equal("error", ((XmlElement)doc.SelectSingleNode("//error[@source='codesize/CyclomaticComplexity']")!).GetAttribute("severity"));
        Assert.Equal("warning", ((XmlElement)doc.SelectSingleNode("//error[@source='codesize/TooManyFields']")!).GetAttribute("severity"));
        Assert.Equal("info", ((XmlElement)doc.SelectSingleNode("//error[@source='naming/ShortVariable']")!).GetAttribute("severity"));
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
        var last = (XmlElement)doc.SelectSingleNode("/checkstyle/file[@name='/src/Bad.cs']")!;
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
