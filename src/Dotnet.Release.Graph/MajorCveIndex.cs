using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

/// <summary>
/// Per-major aggregate of the CVE disclosures affecting a .NET major version.
/// A query accelerator: one fetch answers "what affects 8.0" without crawling
/// every timeline month. The timeline month files remain the canonical home for
/// full disclosure detail; each embedded summary links back to its month.
/// </summary>
[Description("Per-major aggregate index of CVE disclosures affecting a .NET major version")]
public record MajorCveIndex(
    [Description("Concise title for the document")]
    string Title,
    [Description("Major version identifier (e.g., '8.0')")]
    string Version)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Date the index was last generated (e.g., '2026-06-10')")]
    public string? LastUpdated { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("CVE identifiers affecting this major version")]
    public IList<string>? CveRecords { get; init; }

    [JsonPropertyName("_links"),
     Description("HAL+JSON links for hypermedia navigation")]
    public Dictionary<string, HalLink> Links { get; init; } = [];

    [JsonPropertyName("_embedded"),
     Description("Embedded CVE disclosure summaries affecting this major version")]
    public MajorCveIndexEmbedded? Embedded { get; set; }
}

[Description("Container for embedded major-level CVE disclosure summaries")]
public record MajorCveIndexEmbedded
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("CVE disclosure summaries affecting this major version")]
    public IReadOnlyList<CveRecordSummary>? Disclosures { get; set; }
}
