using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

/// <summary>
/// Index of .NET SDK releases organized by feature bands.
/// </summary>
[Description("Index of .NET SDK releases organized by feature bands")]
public record SdkVersionIndex(
    [Description("Type of release document")]
    ReleaseKind Kind,
    [Description("SDK major version (e.g., '8.0', '9.0')")]
    string Version,
    [Description("Concise title for the document")]
    string Title)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Description of the SDK index")]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest SDK version")]
    public string? Latest { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest SDK version with security fixes")]
    public string? LatestSecurity { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest active feature band version")]
    public string? LatestFeatureBand { get; init; }

    [JsonPropertyName("_links"),
     Description("HAL+JSON links for hypermedia navigation")]
    public Dictionary<string, HalLink>? Links { get; init; }

    [JsonPropertyName("_embedded"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Embedded SDK feature band entries")]
    public SdkVersionIndexEmbedded? Embedded { get; set; }
}

[Description("Container for embedded SDK feature band entries")]
public record SdkVersionIndexEmbedded(
    [Description("List of SDK feature band entries")]
    List<SdkFeatureBandEntry> FeatureBands);

[Description("SDK feature band entry with version metadata")]
public record SdkFeatureBandEntry(
    [Description("Latest SDK version in this feature band (e.g., '9.0.307')")]
    string Version,
    [Description("Feature band identifier (e.g., '9.0.3xx')")]
    string Band,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date of the latest SDK in this feature band")]
    DateTimeOffset? Date,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Descriptive label for the feature band")]
    string? Label,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Support phase")]
    SupportPhase? SupportPhase,
    [property: JsonPropertyName("_links"),
     Description("HAL+JSON links for navigation")]
    Dictionary<string, HalLink> Links);

[Description("SDK release entry with version metadata")]
public record SdkReleaseEntry(
    [Description("SDK version (e.g., '8.0.100')")]
    string Version,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date")]
    DateTimeOffset? Date,
    [Description("Whether this release includes security fixes")]
    bool Security,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Support phase at time of release")]
    SupportPhase? SupportPhase,
    [property: JsonPropertyName("_links"),
     Description("HAL+JSON links for navigation")]
    Dictionary<string, HalLink> Links)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("List of CVE IDs addressed in this release")]
    public IReadOnlyList<string>? CveRecords { get; init; }
}

[Description("SDK download information with direct links to installation files")]
public record SdkDownloadInfo(
    [Description("Type of document")]
    ReleaseKind Kind,
    [Description("SDK version (e.g., '8.0.1xx')")]
    string Version,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Support phase")]
    SupportPhase? SupportPhase,
    [Description("Concise title")]
    string Title,
    [Description("Description of the SDK download")]
    string Description,
    [property: JsonPropertyName("_links"),
     Description("HAL+JSON links for navigation")]
    Dictionary<string, HalLink> Links)
{
    [JsonPropertyName("_embedded"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Embedded SDK downloads organized by RID")]
    public SdkDownloadEmbedded? Embedded { get; set; }
}

[Description("Container for SDK downloads keyed by runtime identifier")]
public record SdkDownloadEmbedded(
    [Description("Dictionary of SDK downloads keyed by RID")]
    Dictionary<string, SdkDownloadFile> Downloads);

[Description("Individual SDK download file for a specific platform")]
public record SdkDownloadFile(
    [Description("File name")]
    string Name,
    [Description("Runtime identifier")]
    string Rid,
    [Description("Operating system")]
    string Os,
    [Description("Architecture")]
    string Arch,
    [Description("Hash algorithm (e.g., 'sha512')")]
    string HashAlgorithm,
    [property: JsonPropertyName("_links"),
     Description("Download and hash links")]
    Dictionary<string, HalLink> Links);
