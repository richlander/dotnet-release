using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

/// <summary>
/// Detailed index for a specific patch release (e.g., 9.0.0).
/// </summary>
[Description("Detailed index for a specific patch release")]
public record PatchDetailIndex(
    [Description("Type of release document")]
    ReleaseKind Kind,
    [Description("Concise title for the document")]
    string Title,
    [Description("Patch version identifier (e.g., '8.0.1', '9.0.2')")]
    string Version,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date")]
    DateTimeOffset? Date,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Support phase at time of release")]
    SupportPhase? SupportPhase,
    [property: Description("True if this release includes security fixes")]
    bool Security,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("CVE identifiers fixed in this release")]
    IReadOnlyList<string>? CveRecords)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Description of the patch release")]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date of the previous patch")]
    public DateTimeOffset? PrevPatchDate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date of the previous security patch")]
    public DateTimeOffset? PrevSecurityPatchDate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Highest SDK version shipped with this runtime patch")]
    public string? SdkVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("SDK feature band versions shipped with this runtime patch")]
    public IReadOnlyList<string>? SdkFeatureBands { get; init; }

    [JsonPropertyName("_links"),
     Description("HAL+JSON links for hypermedia navigation")]
    public Dictionary<string, HalLink> Links { get; init; } = [];

    [JsonPropertyName("_embedded"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Embedded runtime, SDK, and documentation")]
    public PatchDetailIndexEmbedded? Embedded { get; set; }
}

[Description("Container for embedded runtime and SDK releases")]
public record PatchDetailIndexEmbedded
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Runtime release with release notes")]
    public RuntimeEntry? Runtime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Highest SDK release as a feature band object")]
    public SdkFeatureBandEntry? Sdk { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("All SDK feature bands shipped with this runtime patch")]
    public IReadOnlyList<SdkFeatureBandEntry>? SdkFeatureBands { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Component-specific documentation")]
    public Dictionary<string, HalLink>? Documentation { get; set; }
}

[Description("Runtime release entry with release notes")]
public record RuntimeEntry(
    [property: Description("Runtime version (same as patch version)")]
    string Version,
    [property: JsonPropertyName("_links"),
     Description("HAL+JSON links for runtime release notes")]
    Dictionary<string, HalLink> Links);
