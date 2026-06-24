using System.Text.Json.Serialization;

namespace MessSharp.Report;

// ---------------------------------------------------------------------------
// SARIF 2.1.0 data model — extracted from SarifRenderer to reduce coupling.
// ---------------------------------------------------------------------------

internal sealed class SarifDocument
{
    [JsonPropertyName("$schema")] public string Schema { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("runs")]    public List<SarifRun> Runs { get; set; } = new();
}

internal sealed class SarifRun
{
    [JsonPropertyName("tool")]    public SarifTool Tool { get; set; } = new();
    [JsonPropertyName("results")] public List<SarifResult> Results { get; set; } = new();
}

internal sealed class SarifTool
{
    [JsonPropertyName("driver")] public SarifDriver Driver { get; set; } = new();
}

internal sealed class SarifDriver
{
    [JsonPropertyName("name")]    public string Name { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("rules")]   public List<SarifRule> Rules { get; set; } = new();
}

internal sealed class SarifRule
{
    [JsonPropertyName("id")]               public string Id { get; set; } = "";
    [JsonPropertyName("name")]             public string Name { get; set; } = "";
    [JsonPropertyName("helpUri")]          public string? HelpUri { get; set; }
    [JsonPropertyName("shortDescription")] public SarifMessage ShortDescription { get; set; } = new();
}

internal sealed class SarifResult
{
    [JsonPropertyName("ruleId")]    public string RuleId { get; set; } = "";
    [JsonPropertyName("level")]     public string Level { get; set; } = "";
    [JsonPropertyName("message")]   public SarifMessage Message { get; set; } = new();
    [JsonPropertyName("locations")] public List<SarifLocation> Locations { get; set; } = new();
}

internal sealed class SarifMessage
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
}

internal sealed class SarifLocation
{
    [JsonPropertyName("physicalLocation")] public SarifPhysicalLocation PhysicalLocation { get; set; } = new();
}

internal sealed class SarifPhysicalLocation
{
    [JsonPropertyName("artifactLocation")] public SarifArtifactLocation ArtifactLocation { get; set; } = new();
    [JsonPropertyName("region")]           public SarifRegion Region { get; set; } = new();
}

internal sealed class SarifArtifactLocation
{
    [JsonPropertyName("uri")] public string Uri { get; set; } = "";
}

internal sealed class SarifRegion
{
    [JsonPropertyName("startLine")] public int StartLine { get; set; }
    [JsonPropertyName("endLine")]   public int EndLine { get; set; }
}
