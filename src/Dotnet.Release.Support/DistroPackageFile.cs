using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Support;

// distros/index.json — plain index of files in the distros directory
[Description("Index of per-distro package files.")]
public record DistrosIndex(
    [property: Description("Major.minor version of .NET (e.g. '11.0').")]
    string ChannelVersion,

    [property: Description("Per-distro file names (e.g. 'ubuntu.json').")]
    IList<string> Distros);

// dependencies.json — distro-agnostic package list extracted from os-packages.json
[Description("Distro-agnostic dependency packages required by .NET.")]
public record DependenciesFile(
    [property: Description("Major.minor version of .NET (e.g. '11.0').")]
    string ChannelVersion,

    [property: Description("Packages required to run .NET on Linux.")]
    IList<DependencyPackage> Packages);

[Description("A distro-agnostic dependency package.")]
public record DependencyPackage(
    [property: Description("Package identifier (e.g. 'libc', 'openssl').")]
    string Id,

    [property: Description("Display name.")]
    string Name,

    [property: Description("Scenarios that require this package (e.g. 'all', 'https', 'globalization').")]
    IList<string> RequiredScenarios,

    [property: Description("Minimum required version (e.g. '1.1.1').")]
    string? MinVersion = null,

    [property: Description("Reference URLs for this package.")]
    IList<string>? References = null);

// Per-distro package file for distros/<distro>.json
// Scoped to the .NET version of the parent release-notes/{version}/ directory.
// Combines dependencies (.NET needs) with dotnet packages (where to get .NET).
[Description("Per-distro package information combining dependencies and .NET package availability for a single .NET version.")]
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

    [property: Description(".NET packages available from the distro's built-in feed.")]
    IList<DotnetComponentPackage>? DotnetPackages = null,

    [property: Description("Alternative feed .NET packages (keyed by feed name, e.g. 'backports', 'microsoft').")]
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
