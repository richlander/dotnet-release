using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Support;

// For os-packages.json
// Example: https://github.com/dotnet/core/blob/main/release-notes/9.0/os-packages.json
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("The set of packages required by a given product version for a set of distros.")]
public record OSPackagesOverview(
    [property: Description("Major (or major.minor) version of product.")]
    string ChannelVersion,

    [property: Description("Set of nominal packages used by product, with descriptions.")]
    IList<Package> Packages,

    [property: Description("Set of distributions where the product can be used.")]
    IList<Distribution> Distributions);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("A nominal package is a distro-agnostic representation of a package.")]
public record Package(
    [property: Description("ID of nominal package.")]
    string Id,

    [property: Description("Display name of nominal package.")]
    string Name,

    [property: Description("Required scenarios for which the package must be used.")]
    IList<Scenario> RequiredScenarios,

    [property: Description("Minimum required version of library.")]
    string? MinVersion = null,

    [property: Description("Related references.")]
    IList<string>? References = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("An operating system distribution, with required package install commands.")]
public record Distribution(
    [Description("Name of the distribution.")]
    string Name,

    [Description("Commands required to install packages.")]
    IList<Command> InstallCommands,

    [Description("Releases for that distribution.")]
    IList<DistroRelease> Releases);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("A command to be run to install packages")]
public record Command(
    [Description("Whether the command needs to be run under sudo.")]
    bool RunUnderSudo,

    [Description("The command to be run, like apt.")]
    string CommandRoot,

    [Description("The command parts or arguments.")]
    IList<string>? CommandParts = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("A distribution release with a list of packages to install.")]
public record DistroRelease(
    [Description("The name of the release.")]
    string Name,

    [Description("The version number for the release.")]
    string Release,

    [Description("The packages required by the distro release.")]
    IList<DistroPackage> Packages);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("A distro archive package to install.")]
public record DistroPackage(
    [property: Description("Reference to nominal package ID.")]
    string Id,

    [property: Description("Package name in the distro archive.")]
    string Name);

[JsonConverter(typeof(KebabCaseLowerStringEnumConverter<Scenario>))]
[Description("Scenarios relating to package dependencies.")]
public enum Scenario
{
    All,
    Runtime,
    Https,
    Cryptography,
    Globalization,
    Kerberos
}
