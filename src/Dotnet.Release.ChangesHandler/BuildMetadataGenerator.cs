using System.Xml.Linq;
using Dotnet.Release.Changes;

namespace Dotnet.Release.ChangesHandler;

/// <summary>
/// Generates build metadata by reading VMR version info and querying NuGet feeds.
/// </summary>
public class BuildMetadataGenerator(NuGetFeedClient nuGetClient)
{
    private static readonly string[] RefPackIds =
    [
        "Microsoft.NETCore.App.Ref",
        "Microsoft.AspNetCore.App.Ref",
        "Microsoft.WindowsDesktop.App.Ref"
    ];

    /// <summary>
    /// Generates build metadata for a VMR repo between two refs.
    /// </summary>
    public async Task<BuildMetadata> GenerateAsync(
        string repoPath, string baseRef, string headRef)
    {
        var versionProps = await VersionPropsReader.ReadAsync(repoPath, headRef);
        var feedUrl = await ReadFeedUrlAsync(repoPath, headRef, versionProps.Major);

        var packages = new Dictionary<string, string>();
        string? buildVersion = null;

        foreach (var packageId in RefPackIds)
        {
            var version = await nuGetClient.GetLatestVersionAsync(
                feedUrl, packageId, versionProps.PreReleaseBranding);

            if (version is not null)
            {
                packages[packageId] = version;
                buildVersion ??= version;
            }
        }

        if (buildVersion is null)
        {
            throw new InvalidOperationException(
                $"No packages found matching '{versionProps.PreReleaseBranding}' " +
                $"on feed {feedUrl}");
        }

        // Derive SDK version: same build suffix, different band
        // Runtime: 11.0.0-preview.3.26179.102 → SDK: 11.0.100-preview.3.26179.102
        var sdkVersion = DeriveSdkVersion(buildVersion, versionProps);
        var sdkUrl = $"https://ci.dot.net/public/Sdk/{sdkVersion}/dotnet-sdk-{sdkVersion}-{{platform}}.tar.gz";

        return new BuildMetadata(
            Version: versionProps.ReleaseVersion,
            BaseRef: baseRef,
            HeadRef: headRef,
            Build: new BuildInfo(
                Version: buildVersion,
                SdkVersion: sdkVersion,
                SdkUrl: sdkUrl
            ),
            NuGet: new NuGetInfo(
                Source: feedUrl,
                Packages: packages
            )
        );
    }

    /// <summary>
    /// Reads the nightly NuGet feed URL from NuGet.config at the given ref.
    /// Falls back to the conventional dotnet{major} feed URL pattern.
    /// </summary>
    internal static async Task<string> ReadFeedUrlAsync(
        string repoPath, string gitRef, int majorVersion)
    {
        var feedName = $"dotnet{majorVersion}";
        var fallbackUrl = $"https://pkgs.dev.azure.com/dnceng/public/_packaging/{feedName}/nuget/v3/index.json";

        try
        {
            var xml = await GitHelpers.ShowFileAsync(repoPath, gitRef, "NuGet.config");
            var doc = XDocument.Parse(xml);

            var entry = doc.Descendants("add")
                .FirstOrDefault(e =>
                    string.Equals(e.Attribute("key")?.Value, feedName, StringComparison.OrdinalIgnoreCase));

            return entry?.Attribute("value")?.Value ?? fallbackUrl;
        }
        catch
        {
            return fallbackUrl;
        }
    }

    /// <summary>
    /// Derives the SDK version from a runtime version and version props.
    /// E.g., "11.0.0-preview.3.26179.102" → "11.0.100-preview.3.26179.102"
    /// </summary>
    internal static string DeriveSdkVersion(string runtimeVersion, VersionProps props)
    {
        // Runtime version: {major}.{minor}.{patch}-{prerelease}.{buildDate}.{buildRev}
        // SDK version: {major}.{minor}.{sdkBand}-{prerelease}.{buildDate}.{buildRev}
        var dashIndex = runtimeVersion.IndexOf('-');
        if (dashIndex < 0)
        {
            return $"{props.Major}.{props.Minor}.{props.SdkBand}";
        }

        var suffix = runtimeVersion[dashIndex..];
        return $"{props.Major}.{props.Minor}.{props.SdkBand}{suffix}";
    }
}
