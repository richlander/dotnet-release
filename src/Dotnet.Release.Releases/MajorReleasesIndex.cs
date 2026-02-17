using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Releases;

// For releases-index.json
// Example: https://github.com/dotnet/core/blob/main/release-notes/releases-index.json
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("A set of major product releases with high-level information.")]
public record MajorReleasesIndex(
    [property: Description("Set of major releases.")]
    IList<MajorReleaseIndexItem> ReleasesIndex);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("A major version release, including the latest patch version.")]
public record MajorReleaseIndexItem(
    [property: Description("Major (or major.minor) version of product.")]
    string ChannelVersion,

    [property: Description("The version of the most recent patch release.")]
    string LatestRelease,

    [property: Description("The date of the most recent patch release.")]
    DateOnly LatestReleaseDate,

    [property: Description("Whether the most recent patch release contains security fixes.")]
    bool Security,

    [property: Description("The runtime version of the most recent patch release.")]
    string LatestRuntime,

    [property: Description("The SDK version of the most recent patch release.")]
    string LatestSdk,

    [property: Description("The product marketing name.")]
    string Product,

    [property: Description("The support phase of the major release.")]
    SupportPhase SupportPhase,

    [property: Description("End of life date of the major release.")]
    DateOnly EolDate,

    [property: Description("The release type of the major release.")]
    ReleaseType ReleaseType,

    [property: Description("Link to detailed release descriptions (JSON format)."),
        JsonPropertyName("releases.json")]
    string ReleasesJson,

    [property: Description("Link to detailed release descriptions (JSON format).")]
    string PatchReleasesInfoUri,

    [property: Description("Link to patch releases index (JSON format)."),
        JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? PatchReleasesIndexUri = null,

    [property: Description("Link to supported OS matrix (JSON format)."),
        JsonPropertyName("supported-os.json"),
        JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SupportedOsJson = null,

    [property: Description("Link to supported OS matrix (JSON format)."),
        JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SupportedOsInfoUri = null,

    [property: Description("Link to OS package information (JSON format)."),
        JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? OsPackagesInfoUri = null);
