using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Releases;

// For release.json
// Example: https://github.com/dotnet/core/blob/main/release-notes/8.0/8.0.1/release.json
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("A patch release overview with channel version.")]
public record PatchReleaseOverview(
    [property: Description("Major (or major.minor) version of product.")]
    string ChannelVersion,

    [property: Description("The patch release.")]
    PatchRelease Release);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("A patch release with runtime, SDK, and component information.")]
public record PatchRelease(
    [property: Description("Release date.")]
    DateOnly ReleaseDate,

    [property: Description("Release version.")]
    string ReleaseVersion,

    [property: Description("Whether this release contains security fixes.")]
    bool Security,

    [property: Description("List of CVEs fixed in this release.")]
    IList<Cve> CveList,

    [property: Description("Release notes URL.")]
    string ReleaseNotes,

    [property: Description("Runtime component information.")]
    RuntimeComponent Runtime,

    [property: Description("SDK component information.")]
    SdkComponent Sdk,

    [property: Description("ASP.NET Core component information.")]
    AspNetCoreComponent AspnetcoreRuntime)
{
    [Description("Windows Desktop component information."),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Component? WindowsDesktop { get; init; }

    [Description("Symbols information."),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Component? Symbols { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("A CVE fixed in a release.")]
public record Cve(
    [property: Description("CVE identifier.")]
    string CveId,

    [property: Description("CVE URL.")]
    string CveUrl);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("Runtime component with version and files.")]
public record RuntimeComponent(
    [property: Description("Component version.")]
    string Version,

    [property: Description("Component version display name.")]
    string VersionDisplay,

    [property: Description("Visual Studio versions.")]
    string VsVersion,

    [property: Description("Visual Studio support.")]
    string VsSupport,

    [property: Description("Download files.")]
    IList<ComponentFile> Files);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("SDK component with version and files.")]
public record SdkComponent(
    [property: Description("Component version.")]
    string Version,

    [property: Description("Component version display name.")]
    string VersionDisplay,

    [property: Description("Runtime version included.")]
    string RuntimeVersion,

    [property: Description("Visual Studio versions.")]
    string VsVersion,

    [property: Description("Visual Studio support.")]
    string VsSupport,

    [property: Description("C# version.")]
    string CsharpVersion,

    [property: Description("F# version.")]
    string FsharpVersion,

    [property: Description("VB version.")]
    string VbVersion,

    [property: Description("Download files.")]
    IList<ComponentFile> Files);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("ASP.NET Core component with version and files.")]
public record AspNetCoreComponent(
    [property: Description("Component version.")]
    string Version,

    [property: Description("Component version display name.")]
    string VersionDisplay,

    [property: Description("ASP.NET Core module versions.")]
    string AspnetcoreModuleVersions,

    [property: Description("Visual Studio version.")]
    string VsVersion,

    [property: Description("Download files.")]
    IList<ComponentFile> Files);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("A generic component with version and files.")]
public record Component(
    [property: Description("Component version.")]
    string Version,

    [property: Description("Component version display name.")]
    string VersionDisplay,

    [property: Description("Download files.")]
    IList<ComponentFile> Files);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("A downloadable component file.")]
public record ComponentFile(
    [property: Description("File name.")]
    string Name,

    [property: Description("Runtime identifier.")]
    string Rid,

    [property: Description("Download URL.")]
    string Url,

    [property: Description("File hash.")]
    string Hash)
{
    [Description("Short aka.ms link."),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Akams { get; init; }
}
