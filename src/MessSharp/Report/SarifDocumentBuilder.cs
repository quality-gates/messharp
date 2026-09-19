using MessSharp.Rule;

namespace MessSharp.Report;

/// <summary>
/// Builds SARIF rule-metadata and result objects from MessSharp violations.
/// Extracted from SarifRenderer to reduce that class's coupling count.
/// </summary>
internal static class SarifDocumentBuilder
{
    internal static SarifRule BuildRule(IRule rule) =>
        new SarifRule
        {
            Id = rule.Name,
            Name = rule.Name,
            HelpUri = string.IsNullOrWhiteSpace(rule.ExternalUrl) ? null : rule.ExternalUrl,
            ShortDescription = new SarifMessage { Text = rule.Description.Trim() },
        };

    internal static SarifResult BuildResult(Violation v) =>
        new SarifResult
        {
            RuleId = v.Rule.Name,
            Level = ViolationLevel(v.Priority),
            Message = new SarifMessage { Text = v.Description },
            Locations = new List<SarifLocation>
            {
                new SarifLocation
                {
                    PhysicalLocation = new SarifPhysicalLocation
                    {
                        ArtifactLocation = new SarifArtifactLocation { Uri = ToUriReference(v.File) },
                        Region = new SarifRegion { StartLine = v.BeginLine, EndLine = v.EndLine },
                    },
                },
            },
        };

    internal static SarifResult BuildErrorResult(ProcessingError e) =>
        new SarifResult
        {
            Level = "error",
            Message = new SarifMessage { Text = e.Message },
            Locations = new List<SarifLocation>
            {
                new SarifLocation
                {
                    PhysicalLocation = new SarifPhysicalLocation
                    {
                        ArtifactLocation = new SarifArtifactLocation { Uri = ToUriReference(e.File) },
                    },
                },
            },
        };

    internal static string ToUriReference(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/');
        var count = segments.Length;
        for (var i = 0; i < count; i++)
        {
            segments[i] = Uri.EscapeDataString(segments[i]);
        }

        return string.Join("/", segments);
    }

    private static string ViolationLevel(int priority) => priority <= 2 ? "error" : "warning";
}
