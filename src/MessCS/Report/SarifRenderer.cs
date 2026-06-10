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

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void Render(TextWriter w, Report report)
    {
        var seen = new HashSet<string>();
        var rules = new List<SarifRule>();
        var results = new List<SarifResult>();

        foreach (var v in report.Violations)
        {
            var id = v.Rule.Name;
            if (seen.Add(id))
            {
                rules.Add(new SarifRule
                {
                    Id = id,
                    Name = id,
                    HelpUri = string.IsNullOrWhiteSpace(v.Rule.ExternalUrl) ? null : v.Rule.ExternalUrl,
                    ShortDescription = new SarifMessage { Text = v.Rule.Description.Trim() },
                });
            }

            results.Add(new SarifResult
            {
                RuleId = id,
                Level = Level(v.Priority),
                Message = new SarifMessage { Text = v.Description },
                Locations = new List<SarifLocation>
                {
                    new SarifLocation
                    {
                        PhysicalLocation = new SarifPhysicalLocation
                        {
                            ArtifactLocation = new SarifArtifactLocation { Uri = v.File },
                            Region = new SarifRegion { StartLine = v.BeginLine, EndLine = v.EndLine },
                        },
                    },
                },
            });
        }

        var doc = new SarifDocument
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

        var json = JsonSerializer.Serialize(doc, _opts);
        w.WriteLine(json);
    }

    private static string Level(int priority) => priority <= 2 ? "error" : "warning";

    private sealed class SarifDocument
    {
        [JsonPropertyName("$schema")] public string Schema { get; set; } = "";
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("runs")] public List<SarifRun> Runs { get; set; } = new();
    }

    private sealed class SarifRun
    {
        [JsonPropertyName("tool")] public SarifTool Tool { get; set; } = new();
        [JsonPropertyName("results")] public List<SarifResult> Results { get; set; } = new();
    }

    private sealed class SarifTool
    {
        [JsonPropertyName("driver")] public SarifDriver Driver { get; set; } = new();
    }

    private sealed class SarifDriver
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("rules")] public List<SarifRule> Rules { get; set; } = new();
    }

    private sealed class SarifRule
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("helpUri")] public string? HelpUri { get; set; }
        [JsonPropertyName("shortDescription")] public SarifMessage ShortDescription { get; set; } = new();
    }

    private sealed class SarifResult
    {
        [JsonPropertyName("ruleId")] public string RuleId { get; set; } = "";
        [JsonPropertyName("level")] public string Level { get; set; } = "";
        [JsonPropertyName("message")] public SarifMessage Message { get; set; } = new();
        [JsonPropertyName("locations")] public List<SarifLocation> Locations { get; set; } = new();
    }

    private sealed class SarifMessage
    {
        [JsonPropertyName("text")] public string Text { get; set; } = "";
    }

    private sealed class SarifLocation
    {
        [JsonPropertyName("physicalLocation")] public SarifPhysicalLocation PhysicalLocation { get; set; } = new();
    }

    private sealed class SarifPhysicalLocation
    {
        [JsonPropertyName("artifactLocation")] public SarifArtifactLocation ArtifactLocation { get; set; } = new();
        [JsonPropertyName("region")] public SarifRegion Region { get; set; } = new();
    }

    private sealed class SarifArtifactLocation
    {
        [JsonPropertyName("uri")] public string Uri { get; set; } = "";
    }

    private sealed class SarifRegion
    {
        [JsonPropertyName("startLine")] public int StartLine { get; set; }
        [JsonPropertyName("endLine")] public int EndLine { get; set; }
    }
}
