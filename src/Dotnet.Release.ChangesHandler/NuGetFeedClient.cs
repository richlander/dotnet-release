using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotnet.Release.ChangesHandler;

/// <summary>
/// Queries NuGet v3 flat container API for package versions from public feeds.
/// </summary>
public class NuGetFeedClient(HttpClient httpClient)
{
    /// <summary>
    /// Gets the latest package version matching a version prefix from a NuGet v3 feed.
    /// Uses the flat container (flat2) API endpoint.
    /// </summary>
    public async Task<string?> GetLatestVersionAsync(
        string feedUrl, string packageId, string versionFilter)
    {
        // Derive flat container URL from feed URL
        // Feed: .../nuget/v3/index.json → Flat: .../nuget/v3/flat2/{id}/index.json
        var baseUrl = feedUrl.Replace("/index.json", "", StringComparison.OrdinalIgnoreCase);
        var url = $"{baseUrl}/flat2/{packageId.ToLowerInvariant()}/index.json";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine(
                $"Warning: NuGet feed returned {response.StatusCode} for {packageId}");
            return null;
        }

        var json = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync(json,
            NuGetSerializerContext.Default.NuGetPackageVersions);

        if (result?.Versions is null or { Count: 0 })
        {
            return null;
        }

        // Filter versions matching the branding (e.g., "preview.3")
        // and take the latest by sorting on the build number suffix
        var matching = result.Versions
            .Where(v => v.Contains(versionFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matching.Count == 0)
        {
            return null;
        }

        // Sort by the numeric build suffix (e.g., "26179.102" → [26179, 102])
        matching.Sort((a, b) => CompareBuildSuffix(a, b));
        return matching[^1];
    }

    /// <summary>
    /// Compares two version strings by their trailing numeric build suffix.
    /// </summary>
    private static int CompareBuildSuffix(string a, string b)
    {
        var aParts = GetBuildSuffix(a);
        var bParts = GetBuildSuffix(b);

        for (int i = 0; i < Math.Min(aParts.Length, bParts.Length); i++)
        {
            var cmp = aParts[i].CompareTo(bParts[i]);
            if (cmp != 0) return cmp;
        }

        return aParts.Length.CompareTo(bParts.Length);
    }

    private static int[] GetBuildSuffix(string version)
    {
        // Version like "11.0.0-preview.3.26179.102" → split on '.', take last two
        var parts = version.Split('.');
        if (parts.Length < 2) return [0];

        var result = new List<int>();
        for (int i = parts.Length - 2; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out var n))
                result.Add(n);
        }
        return [.. result];
    }
}

// NuGet v3 flat container response
internal record NuGetPackageVersions(
    [property: JsonPropertyName("versions")]
    IList<string> Versions
);

[JsonSerializable(typeof(NuGetPackageVersions))]
internal partial class NuGetSerializerContext : JsonSerializerContext
{
}
