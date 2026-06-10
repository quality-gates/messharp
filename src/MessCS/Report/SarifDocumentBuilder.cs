using MessCS.Rule;

namespace MessCS.Report;

/// <summary>
/// Builds SARIF rule-metadata and result objects from MessCS violations.
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
                        ArtifactLocation = new SarifArtifactLocation { Uri = v.File },
                        Region = new SarifRegion { StartLine = v.BeginLine, EndLine = v.EndLine },
                    },
                },
            },
        };

    private static string ViolationLevel(int priority) => priority <= 2 ? "error" : "warning";
}
