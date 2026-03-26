using System.Text.Json;
using Dotnet.Release;
using Dotnet.Release.Releases;

namespace Dotnet.Release.Tools;

/// <summary>
/// Generates releases-index.json from per-major-version releases.json files.
/// </summary>
public static class ReleasesIndexGenerator
{
    private const string CdnBaseUrl = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/";

    public static async Task GenerateAsync(string releasesNotesPath, Stream output, TextWriter log)
    {
        var versions = DiscoverVersions(releasesNotesPath);
        log.WriteLine($"Found {versions.Count} versions: {string.Join(", ", versions)}");

        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WritePropertyName("releases-index");
        writer.WriteStartArray();

        foreach (var version in versions)
        {
            var releasesPath = Path.Combine(releasesNotesPath, version, FileNames.Releases);
            if (!File.Exists(releasesPath))
            {
                log.WriteLine($"  Skipping {version}: no {FileNames.Releases}");
                continue;
            }

            log.WriteLine($"  Reading {version}/{FileNames.Releases}...");
            using var stream = File.OpenRead(releasesPath);
            var overview = await JsonSerializer.DeserializeAsync(stream, MajorReleaseOverviewSerializerContext.Default.MajorReleaseOverview);

            if (overview is null)
            {
                log.WriteLine($"  Skipping {version}: failed to deserialize");
                continue;
            }

            WriteIndexItem(writer, version, overview, releasesNotesPath);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync();
    }

    /// <summary>
    /// Discovers version directories (e.g. "10.0", "9.0") by scanning for directories
    /// whose names parse as decimals, ordered newest-first.
    /// </summary>
    internal static List<string> DiscoverVersions(string basePath)
    {
        return Directory.GetDirectories(basePath)
            .Select(Path.GetFileName)
            .Where(name => name is not null && decimal.TryParse(name, out _))
            .OrderByDescending(name => decimal.Parse(name!))
            .Select(name => name!)
            .ToList();
    }

    static void WriteIndexItem(Utf8JsonWriter writer, string version, MajorReleaseOverview overview, string basePath)
    {
        var latestPatch = overview.Releases.FirstOrDefault();

        writer.WriteStartObject();
        writer.WriteString("channel-version", overview.ChannelVersion);
        writer.WriteString("latest-release", overview.LatestRelease);
        writer.WriteString("latest-release-date", overview.LatestReleaseDate.ToString("yyyy-MM-dd"));
        writer.WriteBoolean("security", latestPatch?.Security ?? false);
        writer.WriteString("latest-runtime", overview.LatestRuntime);
        writer.WriteString("latest-sdk", overview.LatestSdk);
        writer.WriteString("product", InferProduct(version));
        writer.WriteString("support-phase", FormatSupportPhase(overview.SupportPhase));

        if (overview.EolDate != DateOnly.MinValue)
        {
            writer.WriteString("eol-date", overview.EolDate.ToString("yyyy-MM-dd"));
        }

        writer.WriteString("release-type", FormatReleaseType(overview.ReleaseType));
        writer.WriteString("releases.json", $"{CdnBaseUrl}{version}/{FileNames.Releases}");

        var supportedOsPath = Path.Combine(basePath, version, FileNames.SupportedOs);
        if (File.Exists(supportedOsPath))
        {
            writer.WriteString("supported-os.json", $"{CdnBaseUrl}{version}/{FileNames.SupportedOs}");
        }

        writer.WriteEndObject();
    }

    internal static string InferProduct(string version)
    {
        if (decimal.TryParse(version, out var v) && v >= 5.0m) return ".NET";
        return ".NET Core";
    }

    internal static string FormatSupportPhase(SupportPhase phase) => phase switch
    {
        SupportPhase.Preview => "preview",
        SupportPhase.GoLive => "go-live",
        SupportPhase.Active => "active",
        SupportPhase.Maintenance => "maintenance",
        SupportPhase.Eol => "eol",
        _ => phase.ToString().ToLowerInvariant()
    };

    internal static string FormatReleaseType(ReleaseType type) => type switch
    {
        ReleaseType.LTS => "lts",
        ReleaseType.STS => "sts",
        _ => type.ToString().ToLowerInvariant()
    };
}
