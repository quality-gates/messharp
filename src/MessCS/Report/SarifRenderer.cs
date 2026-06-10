using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessCS.Report;

/// <summary>
/// Emits SARIF 2.1.0.
/// Mirrors messgo's SARIFRenderer, using "messcs" as the tool name.
/// </summary>
public sealed class SarifRenderer : IRenderer
{
    private const string ToolName = "messcs";
    private const string ToolVersion = "0.1.0";
    private const string SarifSchemaUri =
        "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void Render(TextWriter w, Report report)
    {
        var (rules, results) = BuildRulesAndResults(report);
        var doc = BuildDocument(rules, results);
        w.WriteLine(JsonSerializer.Serialize(doc, JsonOpts));
    }

    private static (List<SarifRule> rules, List<SarifResult> results)
        BuildRulesAndResults(Report report)
    {
        var seen = new HashSet<string>();
        var rules = new List<SarifRule>();
        var results = new List<SarifResult>();

        foreach (var v in report.Violations)
        {
            if (seen.Add(v.Rule.Name))
                rules.Add(SarifDocumentBuilder.BuildRule(v.Rule));

            results.Add(SarifDocumentBuilder.BuildResult(v));
        }

        return (rules, results);
    }

    private static SarifDocument BuildDocument(List<SarifRule> rules, List<SarifResult> results) =>
        new SarifDocument
        {
            Schema = SarifSchemaUri,
            Version = "2.1.0",
            Runs = new List<SarifRun>
            {
                new SarifRun
                {
                    Tool = new SarifTool
                    {
                        Driver = new SarifDriver
                        {
                            Name = ToolName,
                            Version = ToolVersion,
                            Rules = rules,
                        },
                    },
                    Results = results,
                },
            },
        };
}
