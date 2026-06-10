using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessCS.Report;

/// <summary>
/// Reproduces PHPMD's JSON structure.
/// Mirrors messgo's JSONRenderer.
/// </summary>
public sealed class JsonRenderer : IRenderer
{
    private const string ToolName = "messcs";
    private const string ToolVersion = "0.1.0";

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void Render(TextWriter w, Report report)
    {
        var rep = new JsonReport
        {
            Version = ToolVersion,
            Package = ToolName,
            Timestamp = DateTime.Now.ToString("o"),
            Files = new List<JsonFile>(),
        };

        var idx = new Dictionary<string, int>();
        foreach (var v in report.Violations)
        {
            if (!idx.TryGetValue(v.File, out int i))
            {
                i = rep.Files.Count;
                idx[v.File] = i;
                rep.Files.Add(new JsonFile { File = v.File, Violations = new List<JsonViolation>() });
            }
            rep.Files[i].Violations.Add(new JsonViolation
            {
                BeginLine = v.BeginLine,
                EndLine = v.EndLine,
                Package = v.Package,
                Function = v.Function,
                Class = v.Class,
                Method = v.Method,
                Description = v.Description,
                Rule = v.Rule.Name,
                RuleSet = v.RuleSetName,
                ExternalInfoUrl = v.Rule.ExternalUrl,
                Priority = v.Priority,
            });
        }

        foreach (var e in report.Errors)
        {
            rep.Errors ??= new List<JsonError>();
            rep.Errors.Add(new JsonError { FileName = e.File, Message = e.Message });
        }

        var json = JsonSerializer.Serialize(rep, _opts);
        w.WriteLine(json);
    }

    private sealed class JsonReport
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("package")] public string Package { get; set; } = "";
        [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
        [JsonPropertyName("files")] public List<JsonFile> Files { get; set; } = new();
        [JsonPropertyName("errors")] public List<JsonError>? Errors { get; set; }
    }

    private sealed class JsonFile
    {
        [JsonPropertyName("file")] public string File { get; set; } = "";
        [JsonPropertyName("violations")] public List<JsonViolation> Violations { get; set; } = new();
    }

    private sealed class JsonViolation
    {
        [JsonPropertyName("beginLine")] public int BeginLine { get; set; }
        [JsonPropertyName("endLine")] public int EndLine { get; set; }
        [JsonPropertyName("package")] public string Package { get; set; } = "";
        [JsonPropertyName("function")] public string Function { get; set; } = "";
        [JsonPropertyName("class")] public string Class { get; set; } = "";
        [JsonPropertyName("method")] public string Method { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("rule")] public string Rule { get; set; } = "";
        [JsonPropertyName("ruleSet")] public string RuleSet { get; set; } = "";
        [JsonPropertyName("externalInfoUrl")] public string ExternalInfoUrl { get; set; } = "";
        [JsonPropertyName("priority")] public int Priority { get; set; }
    }

    private sealed class JsonError
    {
        [JsonPropertyName("fileName")] public string FileName { get; set; } = "";
        [JsonPropertyName("message")] public string Message { get; set; } = "";
    }
}
