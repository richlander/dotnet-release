namespace Dotnet.Release.Client;

/// <summary>
/// Well-known base URIs for .NET release data.
/// </summary>
public static class ReleaseNotes
{
    /// <summary>
    /// Official CDN base URI for release metadata.
    /// </summary>
    public const string OfficialBaseUri = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/";

    /// <summary>
    /// GitHub raw content base URI for release notes (release-index branch).
    /// </summary>
    public const string GitHubBaseUri = "https://raw.githubusercontent.com/dotnet/core/release-index/release-notes/";
}
