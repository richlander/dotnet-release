using System.Text.Json;
using Dotnet.Release;
using Dotnet.Release.Releases;
using Markout;
using Markout.Templates;
using MarkdownTable.Formatting;

namespace Dotnet.Release.Tools;

/// <summary>
/// Generates releases.md from per-major-version releases.json files using a Markout template.
/// </summary>
public static class ReleasesGenerator
{
    private const string EmbeddedTemplateName = "Dotnet.Release.Tools.releases-template.md";

    public static MarkoutTemplate LoadTemplate(string? templatePath = null)
    {
        if (templatePath is not null)
            return MarkoutTemplate.Load(templatePath);

        var stream = typeof(ReleasesGenerator).Assembly.GetManifestResourceStream(EmbeddedTemplateName)
            ?? throw new InvalidOperationException($"Embedded template not found: {EmbeddedTemplateName}");

        return MarkoutTemplate.Load(stream);
    }

    public static void ExportTemplate(TextWriter output)
    {
        using var stream = typeof(ReleasesGenerator).Assembly.GetManifestResourceStream(EmbeddedTemplateName)
            ?? throw new InvalidOperationException($"Embedded template not found: {EmbeddedTemplateName}");
        using var reader = new StreamReader(stream);
        output.Write(reader.ReadToEnd());
    }

    public static async Task GenerateAsync(string releasesNotesPath, TextWriter output, TextWriter log, string? templatePath = null)
    {
        var versions = ReleasesIndexGenerator.DiscoverVersions(releasesNotesPath);
        var releases = new List<ReleaseInfo>();

        foreach (var version in versions)
        {
            var releasesPath = Path.Combine(releasesNotesPath, version, FileNames.Releases);
            if (!File.Exists(releasesPath)) continue;

            log.WriteLine($"  Reading {version}/{FileNames.Releases}...");
            using var stream = File.OpenRead(releasesPath);
            var overview = await JsonSerializer.DeserializeAsync(stream, MajorReleaseOverviewSerializerContext.Default.MajorReleaseOverview);
            if (overview is null) continue;

            releases.Add(new ReleaseInfo(version, overview));
        }

        var supported = releases.Where(r => r.Overview.SupportPhase != SupportPhase.Eol).ToList();
        var eol = releases.Where(r => r.Overview.SupportPhase == SupportPhase.Eol).ToList();

        var template = LoadTemplate(templatePath);
        template.TableOptions = new TableFormatterOptions { AutoTune = true };

        template.Bind("supported", new SupportedReleasesBinding(supported));

        if (eol.Count > 0)
        {
            template.Bind("eol", new EolReleasesBinding(eol));
        }

        template.SkipUnboundPlaceholders = true;

        var options = new MarkoutWriterOptions
        {
            PrettyTables = true,
            TableOptions = new() { AutoTune = true }
        };

        output.WriteLine(template.Render(options));
    }

    internal record ReleaseInfo(string Version, MajorReleaseOverview Overview);

    /// <summary>
    /// Renders the supported releases table with columns:
    /// Version | Release Date | Release type | Support phase | Latest Patch Version | End of Support
    /// </summary>
    private class SupportedReleasesBinding(List<ReleaseInfo> releases) : IMarkoutFormattable
    {
        public void WriteTo(MarkoutWriter writer)
        {
            List<string> linkDefs = [];

            writer.WriteTableStart("Version", "Release Date", "Release type", "Support phase", "Latest Patch Version", "End of Support");

            foreach (var info in releases)
            {
                var overview = info.Overview;
                var latestPatch = overview.Releases.FirstOrDefault();
                string product = ReleasesIndexGenerator.InferProduct(info.Version);
                string displayVersion = $"{product} {overview.ChannelVersion}";
                string versionCell = $"[{displayVersion}](./{overview.ChannelVersion}/README.md)";

                string gaDate = GetGaDate(overview);

                string releaseType = $"[{overview.ReleaseType.ToString().ToUpperInvariant()}][policies]";
                string supportPhase = FormatSupportPhase(overview.SupportPhase);
                string patchVersion = overview.LatestRelease;

                string patchLink = latestPatch is not null
                    ? GetPatchNotesPath(latestPatch, overview.ChannelVersion)
                    : $"./{overview.ChannelVersion}/{patchVersion}/{patchVersion}.md";

                linkDefs.Add($"[{patchVersion}]: {patchLink}");
                string patchCell = $"[{patchVersion}][{patchVersion}]";

                string eolDate = overview.EolDate != DateOnly.MinValue
                    ? FormatDate(overview.EolDate)
                    : "TBD";

                writer.WriteTableRow(versionCell, gaDate, releaseType, supportPhase, patchCell, eolDate);
            }

            writer.WriteTableEnd();
            writer.WriteLinkDefinitions(linkDefs.ToArray());
        }
    }

    /// <summary>
    /// Renders the end-of-life releases table with columns:
    /// Version | Release Date | Support | Final Patch Version | End of Support
    /// </summary>
    private class EolReleasesBinding(List<ReleaseInfo> releases) : IMarkoutFormattable
    {
        public void WriteTo(MarkoutWriter writer)
        {
            List<string> linkDefs = [];

            writer.WriteTableStart("Version", "Release Date", "Support", "Final Patch Version", "End of Support");

            foreach (var info in releases)
            {
                var overview = info.Overview;
                var latestPatch = overview.Releases.FirstOrDefault();
                string product = ReleasesIndexGenerator.InferProduct(info.Version);
                string displayVersion = $"{product} {overview.ChannelVersion}";
                string versionCell = $"[{displayVersion}](./{overview.ChannelVersion}/README.md)";

                string gaDate = GetGaDate(overview);

                string releaseType = $"[{overview.ReleaseType.ToString().ToUpperInvariant()}][policies]";
                string patchVersion = overview.LatestRelease;

                string patchLink = latestPatch is not null
                    ? GetPatchNotesPath(latestPatch, overview.ChannelVersion)
                    : $"./{overview.ChannelVersion}/{patchVersion}/{patchVersion}.md";

                linkDefs.Add($"[{patchVersion}]: {patchLink}");
                string patchCell = $"[{patchVersion}][{patchVersion}]";

                string eolDate = overview.EolDate != DateOnly.MinValue
                    ? FormatDate(overview.EolDate)
                    : "-";

                writer.WriteTableRow(versionCell, gaDate, releaseType, patchCell, eolDate);
            }

            writer.WriteTableEnd();
            writer.WriteLinkDefinitions(linkDefs.ToArray());
        }
    }

    static string GetGaDate(MajorReleaseOverview overview)
    {
        if (overview.Releases.Count == 0) return "-";

        // Find the GA release (e.g., "10.0.0" — no preview/rc suffix)
        var gaRelease = overview.Releases.FirstOrDefault(r =>
            !r.ReleaseVersion.Contains("preview", StringComparison.OrdinalIgnoreCase) &&
            !r.ReleaseVersion.Contains("rc", StringComparison.OrdinalIgnoreCase) &&
            r.ReleaseVersion.EndsWith(".0"));

        if (gaRelease is not null)
        {
            return FormatDate(gaRelease.ReleaseDate);
        }

        // Fallback: earliest release date
        return FormatDate(overview.Releases[^1].ReleaseDate);
    }

    static string GetPatchNotesPath(PatchRelease release, string channelVersion)
    {
        const string marker = "release-notes/";
        int idx = release.ReleaseNotes.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return "./" + release.ReleaseNotes[(idx + marker.Length)..];
        }

        return $"./{channelVersion}/{release.ReleaseVersion}/{release.ReleaseVersion}.md";
    }

    static string FormatDate(DateOnly date) => date.ToString("MMMM d, yyyy");

    static string FormatSupportPhase(SupportPhase phase) => phase.ToDisplayName();
}
