using System.Text.Json;
using Dotnet.Release.Cve;

namespace Dotnet.Release.CveHandler;

/// <summary>
/// Loads CVE data from JSON files.
/// </summary>
public static class CveLoader
{
    /// <summary>
    /// Loads CVE records from a cve.json file.
    /// </summary>
    public static async Task<CveRecords?> LoadAsync(string cveJsonPath)
    {
        if (!File.Exists(cveJsonPath))
        {
            return null;
        }

        using var stream = File.OpenRead(cveJsonPath);
        return await JsonSerializer.DeserializeAsync(stream, CveSerializerContext.Default.CveRecords);
    }

    /// <summary>
    /// Loads CVE records from a directory containing cve.json.
    /// </summary>
    public static Task<CveRecords?> LoadFromDirectoryAsync(string directoryPath)
    {
        var cveJsonPath = Path.Combine(directoryPath, "cve.json");
        return LoadAsync(cveJsonPath);
    }

    /// <summary>
    /// Loads CVE records from the timeline directory for a specific release date.
    /// </summary>
    public static Task<CveRecords?> LoadForReleaseDateAsync(string releaseNotesRoot, DateTimeOffset releaseDate)
    {
        var year = releaseDate.Year.ToString("D4");
        var month = releaseDate.Month.ToString("D2");
        var timelinePath = Path.Combine(releaseNotesRoot, "timeline", year, month);
        return LoadFromDirectoryAsync(timelinePath);
    }

    /// <summary>
    /// Finds all cve.json files under a path (single file or directory tree).
    /// </summary>
    public static List<string> FindCveFiles(string path)
    {
        if (File.Exists(path))
        {
            return path.EndsWith("cve.json", StringComparison.OrdinalIgnoreCase)
                ? [path]
                : [];
        }

        if (Directory.Exists(path))
        {
            var files = Directory.GetFiles(path, "cve.json", SearchOption.AllDirectories).ToList();
            files.Sort(StringComparer.Ordinal);
            return files;
        }

        return [];
    }

    /// <summary>
    /// Deserializes CVE records from a stream.
    /// </summary>
    public static ValueTask<CveRecords?> DeserializeAsync(Stream json) =>
        JsonSerializer.DeserializeAsync(json, CveSerializerContext.Default.CveRecords);
}
