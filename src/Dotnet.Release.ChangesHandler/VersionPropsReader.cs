using System.Xml.Linq;

namespace Dotnet.Release.ChangesHandler;

/// <summary>
/// Reads version properties from the VMR's eng/Versions.props file.
/// </summary>
public static class VersionPropsReader
{
    /// <summary>
    /// Reads eng/Versions.props from a local git repo at a given ref.
    /// </summary>
    public static async Task<VersionProps> ReadAsync(string repoPath, string gitRef)
    {
        var xml = await GitHelpers.ShowFileAsync(repoPath, gitRef, "eng/Versions.props");
        return Parse(xml);
    }

    internal static VersionProps Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var props = doc.Descendants("PropertyGroup")
            .SelectMany(pg => pg.Elements())
            .ToDictionary(e => e.Name.LocalName, e => e.Value, StringComparer.OrdinalIgnoreCase);

        return new VersionProps(
            Major: int.Parse(GetRequired(props, "VersionMajor")),
            Minor: int.Parse(GetRequired(props, "VersionMinor")),
            SdkMinor: int.Parse(GetOrDefault(props, "VersionSDKMinor", "1")),
            SdkMinorPatch: int.Parse(GetOrDefault(props, "VersionSDKMinorPatch", "0")),
            PreReleaseLabel: GetOrDefault(props, "PreReleaseVersionLabel", ""),
            PreReleaseIteration: GetOrDefault(props, "PreReleaseVersionIteration", "")
        );
    }

    private static string GetRequired(Dictionary<string, string> props, string key)
        => props.TryGetValue(key, out var value)
            ? value
            : throw new InvalidOperationException($"Missing required property '{key}' in eng/Versions.props");

    private static string GetOrDefault(Dictionary<string, string> props, string key, string defaultValue)
        => props.TryGetValue(key, out var value) ? value : defaultValue;
}

/// <summary>
/// Parsed version properties from eng/Versions.props.
/// </summary>
public record VersionProps(
    int Major,
    int Minor,
    int SdkMinor,
    int SdkMinorPatch,
    string PreReleaseLabel,
    string PreReleaseIteration)
{
    /// <summary>
    /// The preview branding string, e.g. "preview.3". Empty for stable releases.
    /// </summary>
    public string PreReleaseBranding => string.IsNullOrEmpty(PreReleaseLabel)
        ? ""
        : $"{PreReleaseLabel}.{PreReleaseIteration}";

    /// <summary>
    /// The release version, e.g. "11.0.0-preview.3" or "11.0.0".
    /// </summary>
    public string ReleaseVersion => string.IsNullOrEmpty(PreReleaseBranding)
        ? $"{Major}.{Minor}.0"
        : $"{Major}.{Minor}.0-{PreReleaseBranding}";

    /// <summary>
    /// The SDK band, e.g. "100".
    /// </summary>
    public string SdkBand => $"{SdkMinor}{SdkMinorPatch}0";
}
