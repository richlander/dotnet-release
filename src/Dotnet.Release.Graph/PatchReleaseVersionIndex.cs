using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

/// <summary>
/// Provides an index of patch .NET releases within a major version (e.g., 8.0.1, 8.0.2).
/// </summary>
[Description("Index of patch .NET releases with simplified lifecycle information")]
public record PatchReleaseVersionIndex(
    [property: Description("Type of release document")]
    ReleaseKind Kind,
    [property: Description("Concise title for the document")]
    string Title) : IReleaseVersionIndex
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Description of the index scope")]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Target framework moniker for this version (e.g., 'net10.0')")]
    public string? TargetFramework { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest patch version (e.g., '9.0.11')")]
    public string? LatestPatch { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date of the latest patch")]
    public DateTimeOffset? LatestPatchDate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest patch version with security fixes")]
    public string? LatestSecurityPatch { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date of the latest security patch")]
    public DateTimeOffset? LatestSecurityPatchDate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release type: lts or sts")]
    public ReleaseType? ReleaseType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Current support phase")]
    public SupportPhase? SupportPhase { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Whether this version is currently supported")]
    public bool? Supported { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("General Availability date")]
    public DateTimeOffset? GaDate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("End of Life date")]
    public DateTimeOffset? EolDate { get; init; }

    [JsonPropertyName("_links"),
     Description("HAL+JSON links for hypermedia navigation")]
    public Dictionary<string, HalLink> Links { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Usage information and term definitions")]
    public UsageWithLinks? Usage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Glossary of terms and definitions")]
    public Dictionary<string, string>? Glossary { get; set; }

    [JsonPropertyName("_embedded"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Embedded patch release entries")]
    public PatchReleaseVersionIndexEmbedded? Embedded { get; set; }
}

[Description("Container for embedded patch release entries")]
public record PatchReleaseVersionIndexEmbedded(
    [Description("List of patch release entries")]
    List<PatchReleaseVersionIndexEntry> Patches)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("CVE IDs affecting this major version")]
    public IReadOnlyList<string>? CveRecords { get; set; }

    [JsonPropertyName("sdk_feature_bands"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("SDK feature bands for this major version (8.0+)")]
    public IReadOnlyList<SdkFeatureBandEntry>? SdkFeatureBands { get; init; }
}

[Description("Patch release entry within a major version or month index")]
public record PatchReleaseVersionIndexEntry(
    [property: Description("Patch version identifier (e.g., '8.0.1', '9.0.2')")]
    string Version,
    [property: Description("Release date")]
    DateTimeOffset Date,
    [property: Description("Release year (e.g., '2025')")]
    string Year,
    [property: Description("Release month (e.g., '10')")]
    string Month,
    [property: Description("True if this release includes security fixes")]
    bool Security,
    [property: Description("Support phase at time of release")]
    SupportPhase SupportPhase)
{
    [JsonPropertyName("major_release"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Major version this patch belongs to (included in month-index, omitted in major-version-index)")]
    public string? MajorRelease { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Highest SDK version included in this patch release")]
    public string? SdkVersion { get; init; }

    [JsonPropertyName("_links"),
     Description("HAL+JSON links for navigation")]
    public Dictionary<string, HalLink> Links { get; init; } = [];
}
