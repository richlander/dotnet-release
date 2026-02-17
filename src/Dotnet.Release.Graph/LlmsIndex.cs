using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

/// <summary>
/// AI-optimized index providing quick access to latest releases and security information.
/// </summary>
[Description("AI-optimized .NET release index with latest patches and security information")]
public record LlmsIndex(
    [property: Description("Type of release document")]
    ReleaseKind Kind,
    [property: Description("Concise title for the document")]
    string Title)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Note for AI assistants on how to navigate this graph")]
    public string? AiNote { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("URL to required pre-reading for optimal graph navigation")]
    public string? RequiredPreRead { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest major .NET version")]
    public string? LatestMajor { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest LTS major .NET version")]
    public string? LatestLtsMajor { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date of the latest patch across all supported releases")]
    public DateTimeOffset? LatestPatchDate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date of the latest security patch across all supported releases")]
    public DateTimeOffset? LatestSecurityPatchDate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Date when this index was last generated")]
    public DateTimeOffset? LastUpdatedDate { get; init; }

    [JsonPropertyName("supported_major_releases"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Supported major version identifiers")]
    public IReadOnlyList<string>? SupportedMajorReleases { get; init; }

    [JsonPropertyName("_workflows"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Inline navigation workflows for common queries")]
    public Dictionary<string, LlmsWorkflow>? Workflows { get; init; }

    [JsonPropertyName("_links"),
     Description("HAL+JSON links for hypermedia navigation")]
    public Dictionary<string, HalLink> Links { get; init; } = [];

    [JsonPropertyName("_embedded"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Embedded latest patches and security status")]
    public LlmsIndexEmbedded? Embedded { get; init; }
}

[Description("Container for embedded collections in the LLMs index")]
public record LlmsIndexEmbedded
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Current patch for each supported release, keyed by major version")]
    public Dictionary<string, LlmsPatchEntry>? Patches { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Last 3 security months (most recent first)")]
    public IReadOnlyList<HistoryMonthSummary>? LatestSecurityMonths { get; init; }
}

[Description("Patch entry optimized for AI consumption")]
public record LlmsPatchEntry(
    [property: Description("Full patch version (e.g., '9.0.10')")]
    string Version,
    [property: Description("Release type: lts or sts")]
    ReleaseType ReleaseType,
    [property: Description("Whether this release includes security fixes")]
    bool Security,
    [property: Description("Current support phase")]
    SupportPhase SupportPhase,
    [property: Description("Whether this release is currently supported")]
    bool Supported,
    [property: Description("SDK version shipped with this runtime patch")]
    string SdkVersion,
    [property: Description("Latest security patch version for this release")]
    string LatestSecurityPatch,
    [property: Description("Release date of the latest security patch")]
    DateTimeOffset LatestSecurityPatchDate,
    [property: JsonPropertyName("_links"),
     Description("HAL+JSON links")]
    Dictionary<string, HalLink> Links);

[Description("Navigation workflow for common queries")]
public record LlmsWorkflow
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("What this workflow does")]
    public string? Description { get; init; }

    [JsonPropertyName("follow_path"),
     Description("Route to destination as link relations")]
    public IReadOnlyList<string> FollowPath { get; init; } = [];

    [JsonPropertyName("destination_kind"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Document kind at destination")]
    public string? DestinationKind { get; init; }

    [JsonPropertyName("select_embedded"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Properties to extract from _embedded")]
    public IReadOnlyList<string>? SelectEmbedded { get; init; }

    [JsonPropertyName("select_property"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Top-level properties to extract")]
    public IReadOnlyList<string>? SelectProperty { get; init; }

    [JsonPropertyName("select_link"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Link hrefs to return without following")]
    public IReadOnlyList<string>? SelectLink { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("What data this workflow yields")]
    public WorkflowYields? Yields { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Path contains {placeholder} variables")]
    public bool? Templated { get; init; }

    [JsonPropertyName("query_hints"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Sample queries this workflow answers")]
    public IReadOnlyList<string>? QueryHints { get; init; }

    [JsonPropertyName("_links"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Reference to full workflow catalog")]
    public Dictionary<string, HalLink>? Links { get; init; }
}

[Description("Data yielded by a workflow")]
public record WorkflowYields
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("JSONPath-like expression for the data to extract")]
    public string? Data { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Specific fields to include")]
    public IReadOnlyList<string>? Fields { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Filter condition to apply")]
    public string? Filter { get; init; }
}
