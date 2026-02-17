using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Releases;

// For releases.json
// Example: https://github.com/dotnet/core/blob/main/release-notes/8.0/releases.json
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("A major product release with all patch releases.")]
public record MajorReleaseOverview(
    [property: Description("Major (or major.minor) version of product.")]
    string ChannelVersion,

    [property: Description("The version of the most recent patch release.")]
    string LatestRelease,

    [property: Description("The date of the most recent patch release.")]
    DateOnly LatestReleaseDate,

    [property: Description("The runtime version of the most recent patch release.")]
    string LatestRuntime,

    [property: Description("The SDK version of the most recent patch release.")]
    string LatestSdk,

    [property: Description("The support phase of the major release.")]
    SupportPhase SupportPhase,

    [property: Description("The release type of the major release.")]
    ReleaseType ReleaseType,

    [property: Description("End of life date of the major release.")]
    DateOnly EolDate,

    [property: Description("Link to the lifecycle policy.")]
    string LifecyclePolicy,

    [property: Description("All patch releases.")]
    IList<PatchRelease> Releases)
{
    [Description("Intellisense file information."),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Intellisense? Intellisense { get; init; }
}

[Description("Intellisense file listing.")]
public record Intellisense(
    [property: Description("Intellisense version.")]
    string Version,

    [property: Description("Intellisense version display name.")]
    string VersionDisplay,

    [property: Description("Intellisense files.")]
    IList<IntellisenseFile> Files);

[Description("An intellisense file with download URL.")]
public record IntellisenseFile(
    [property: Description("Language identifier.")]
    string Language,

    [property: Description("File name.")]
    string Name,

    [property: Description("Runtime identifier.")]
    string Rid,

    [property: Description("Download URL.")]
    string Url,

    [property: Description("File hash.")]
    string Hash);
