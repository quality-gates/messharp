using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessCS.Rule;

namespace MessCS.Report;

/// <summary>
/// Emits the GitLab Code Quality JSON format.
/// Mirrors messgo's GitLabRenderer, including the fingerprint hashing approach
/// (hex encoding of raw UTF-8 bytes of "file:line:ruleName").
/// </summary>
public sealed class GitLabRenderer : IRenderer
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public void Render(TextWriter w, Report report)
    {
        var entries = new List<GitLabEntry>(report.Violations.Count);
        foreach (var v in report.Violations)
        {
            entries.Add(new GitLabEntry
            {
                Type = "issue",
                CheckName = v.Rule.Name,
                Description = v.Description,
                Fingerprint = Fingerprint(v),
                Severity = Severity(v.Priority),
                Location = new GitLabLocation
                {
                    Path = v.File,
                    Lines = new GitLabLines { Begin = v.BeginLine },
                },
            });
        }

        var json = JsonSerializer.Serialize(entries, _opts);
        w.WriteLine(json);
    }

    /// <summary>
    /// Mirrors messgo: hex-encode the UTF-8 bytes of "file:line:ruleName".
    /// This is NOT a hash — it's a direct hex encoding of the raw bytes,
    /// exactly as Go's fmt.Sprintf("%x", bytes) does.
    /// </summary>
    internal static string Fingerprint(Violation v)
    {
        var raw = Encoding.UTF8.GetBytes($"{v.File}:{v.BeginLine}:{v.Rule.Name}");
        return Convert.ToHexString(raw).ToLowerInvariant();
    }

    private static string Severity(int priority) => priority switch
    {
        1 => "blocker",
        2 => "critical",
        3 => "major",
        4 => "minor",
        _ => "info",
    };

    private sealed class GitLabEntry
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("check_name")] public string CheckName { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("fingerprint")] public string Fingerprint { get; set; } = "";
        [JsonPropertyName("severity")] public string Severity { get; set; } = "";
        [JsonPropertyName("location")] public GitLabLocation Location { get; set; } = new();
    }

    private sealed class GitLabLocation
    {
        [JsonPropertyName("path")] public string Path { get; set; } = "";
        [JsonPropertyName("lines")] public GitLabLines Lines { get; set; } = new();
    }

    private sealed class GitLabLines
    {
        [JsonPropertyName("begin")] public int Begin { get; set; }
    }
}
