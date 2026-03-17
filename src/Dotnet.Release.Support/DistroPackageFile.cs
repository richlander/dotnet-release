using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Support;

// Per-distro package file for distro-packages/<distro>.json
// Combines dependencies (.NET needs) with dotnet packages (where to get .NET)
[Description("Per-distro package information for a given .NET version.")]
public record DistroPackageFile(
    [property: Description("Distribution name.")]
    string Name,

    [property: Description("Command to install dependency packages (e.g. 'apt-get install -y {packages}').")]
    string InstallCommand,

    [property: Description("Releases of this distribution.")]
    IList<DistroPackageRelease> Releases);

[Description("A distribution release with dependencies and .NET package availability.")]
public record DistroPackageRelease(
    [property: Description("Display name for the release.")]
    string Name,

    [property: Description("Version string for the release.")]
    string Release,

    [property: Description("Packages required to run .NET on this release.")]
    IList<DistroDepPackage> Dependencies,

    [property: Description("Built-in .NET packages available in the distro archive.")]
    IList<DotnetComponentPackage>? DotnetPackages = null,

    [property: Description("Alternative feed .NET packages (keyed by feed name).")]
    IDictionary<string, DotnetAlternativeFeed>? DotnetPackagesOther = null,

    [property: Description("Notes about this release (e.g. SDK band limitations).")]
    IList<string>? Notes = null);

[Description("A dependency package required by .NET.")]
public record DistroDepPackage(
    [property: Description("Reference to nominal package ID (e.g. 'libicu').")]
    string Id,

    [property: Description("Package name in the distro archive (e.g. 'libicu78').")]
    string Name);

[Description("A .NET component package available in a feed.")]
public record DotnetComponentPackage(
    [property: Description("Component identifier (e.g. 'sdk', 'runtime', 'aspnetcore_runtime').")]
    string Component,

    [property: Description("Package name in the archive.")]
    string Name);

[Description("An alternative package feed with setup instructions.")]
public record DotnetAlternativeFeed(
    [property: Description("Command to register this feed.")]
    string InstallCommand,

    [property: Description(".NET packages available from this feed.")]
    IList<DotnetComponentPackage> Packages);
