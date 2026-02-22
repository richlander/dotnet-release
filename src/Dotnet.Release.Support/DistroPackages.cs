using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Support;

// For distro-packages.json — documents which .NET packages are available
// in each Linux distribution for a given .NET version, from both the
// distribution's native archive and the Microsoft (packages.microsoft.com) feed.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("Availability of .NET packages in Linux distribution archives for a given .NET version.")]
public record DistroPackagesOverview(
    [property: Description("Major.minor version of .NET (e.g. '9.0').")]
    string ChannelVersion,

    [property: Description("Date when this file was last verified.")]
    DateOnly LastVerified,

    [property: Description(".NET component packages to look for in each distribution.")]
    IList<DotnetComponent> Components,

    [property: Description("Distributions and which .NET packages they offer.")]
    IList<DistroPackageAvailability> Distributions);

[Description("A .NET component that may be packaged by distributions.")]
public record DotnetComponent(
    [property: Description("Component identifier (e.g. 'sdk', 'runtime', 'aspnetcore-runtime').")]
    string Id,

    [property: Description("Display name.")]
    string Name);

[Description("A distribution and its available .NET packages.")]
public record DistroPackageAvailability(
    [property: Description("Distribution name, matching os-packages.json and supported-os.json.")]
    string Name,

    [property: Description("Releases of this distribution with .NET package availability.")]
    IList<DistroReleasePackages> Releases);

[Description("A distribution release and which .NET packages it offers.")]
public record DistroReleasePackages(
    [property: Description("Display name for the release.")]
    string Name,

    [property: Description("Version string for the release.")]
    string Release,

    [property: Description(".NET packages by feed name (e.g. 'builtin', 'backports', 'microsoft'). Each feed maps to a list of available packages.")]
    IDictionary<string, IList<DotnetDistroPackage>>? Feeds = null);

[Description("A .NET package available in a package archive.")]
public record DotnetDistroPackage(
    [property: Description("Component ID (e.g. 'sdk', 'runtime').")]
    string ComponentId,

    [property: Description("Package name in the archive.")]
    string PackageName,

    [property: Description("Package version string from the archive.")]
    string? Version = null,

    [property: Description("Architectures the package is available for.")]
    IList<string>? Architectures = null,

    [property: Description("Repository within the distribution (e.g. 'main', 'universe', 'community').")]
    string? Repository = null);
