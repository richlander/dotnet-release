using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Support;

[Description("Collection of distribution package source metadata.")]
public record DistroSourceCollection(
    [property: Description("Distribution package sources.")]
    IList<DistroSource> Sources);

[Description("A supplemental .NET package feed not covered by pkgs.org.")]
public record DotnetFeed(
    [property: Description("Feed checker type (e.g. 'launchpad', 'brew_formula', 'nixpkgs_github').")]
    string Type,

    [property: Description("URL template for the feed. Supports {major}, {minor}, {version}, {codename} placeholders.")]
    string Url);

[Description("Package source metadata for a Linux distribution family.")]
public record DistroSource(
    [property: Description("Name of the distribution.")]
    string Name,

    [property: Description("Docker image template. Supports {version} and {codename} placeholders.")]
    string? DockerImage = null)
{
    [Description("Product ID on endoflife.date (e.g. 'ubuntu', 'alpine', 'fedora').")]
    public string? EndoflifeId { get; init; }

    [Description("Map of version numbers to distribution codenames (e.g. '24.04' -> 'noble').")]
    public IDictionary<string, string>? Codenames { get; init; }

    [Description("Package naming pattern for .NET packages (e.g. 'dotnet-{component}-{major}.{minor}').")]
    public string? DotnetPackagePattern { get; init; }

    [Description("Supplemental .NET package feeds not covered by pkgs.org (e.g. Ubuntu backports PPA, Homebrew, NixOS).")]
    public IDictionary<string, DotnetFeed>? DotnetFeeds { get; init; }

    /// <summary>
    /// Resolves a Docker image name for the given version.
    /// </summary>
    public string? GetDockerImageName(string version)
    {
        if (DockerImage is null) return null;
        string image = DockerImage.Replace("{version}", version);

        if (Codenames is not null && Codenames.TryGetValue(version, out string? codename))
        {
            image = image.Replace("{codename}", codename);
        }

        return image;
    }

    /// <summary>
    /// Returns the codename for a version, or null if not mapped.
    /// </summary>
    public string? GetCodename(string version) =>
        Codenames is not null && Codenames.TryGetValue(version, out string? codename) ? codename : null;

    /// <summary>
    /// Resolves a feed URL template with version/codename/dotnet version placeholders.
    /// </summary>
    public static string ResolveFeedUrl(string urlTemplate, string? distroVersion, string? codename, string dotnetMajor, string dotnetMinor)
    {
        string url = urlTemplate
            .Replace("{major}", dotnetMajor)
            .Replace("{minor}", dotnetMinor);

        if (distroVersion is not null)
            url = url.Replace("{version}", distroVersion);
        if (codename is not null)
            url = url.Replace("{codename}", codename);

        return url;
    }

    /// <summary>
    /// Loads the embedded distro-sources.json.
    /// </summary>
    public static DistroSourceCollection Load()
    {
        const string resourceName = "Dotnet.Release.Support.distro-sources.json";
        using var stream = typeof(DistroSource).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        return JsonSerializer.Deserialize(stream, DistroSourceSerializerContext.Default.DistroSourceCollection)
            ?? throw new InvalidOperationException("Failed to deserialize distro-sources.json");
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(DistroSourceCollection))]
public partial class DistroSourceSerializerContext : JsonSerializerContext
{
}
