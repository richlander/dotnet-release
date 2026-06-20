using System.Globalization;
using System.Text;
using System.Text.Json;
using Dotnet.Release;
using Dotnet.Release.Cve;
using Dotnet.Release.CveHandler;
using Dotnet.Release.Graph;

namespace Dotnet.Release.IndexGenerator;

/// <summary>
/// Generates the per-major CVE aggregate files: <c>{major}/cve-index.json</c> (a slim,
/// query-fast index of every CVE disclosure affecting the major version) and the
/// human-readable <c>{major}/cve.md</c> that mirrors it.
///
/// The cve-index.json is intentionally slim: it inlines high-signal query fields
/// (id, title, fixed, affected_releases, cvss, disclosure_date, aliases) and points
/// back to the canonical timeline month for heavy detail (description, cvss vector,
/// cna, per-package affected lists, commits).
/// </summary>
public static class CveIndexFiles
{
    public static async Task GenerateAsync(
        string majorVersion,
        IReadOnlyList<ReleaseVersionIndexEntry> patchEntries,
        string inputRoot,
        string outputMajorVersionDir)
    {
        // Patches are ordered newest-first. Walk them, load each month's cve.json, and
        // collect the disclosures that affect this major version.
        var disclosures = new List<CveRecordSummary>();
        var cveIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DateTimeOffset? lastUpdated = null;

        foreach (var entry in patchEntries)
        {
            if (entry.CveRecords is null or { Count: 0 })
            {
                continue;
            }

            var gaDate = entry.Lifecycle?.GaDate;
            if (gaDate is null)
            {
                continue;
            }

            var date = gaDate.Value;
            lastUpdated ??= date;
            var year = date.Year.ToString("D4");
            var month = date.Month.ToString("D2");

            var cveRecords = await CveLoader.LoadForReleaseDateAsync(inputRoot, date);
            if (cveRecords?.Disclosures is null or { Count: 0 })
            {
                continue;
            }

            // Authoritative set of CVE IDs that affect this major in this month.
            IEnumerable<string> idsForMajor = cveRecords.ReleaseCves?.TryGetValue(majorVersion, out var ids) == true
                ? ids
                : entry.CveRecords;

            var idSet = idsForMajor.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var disclosure in cveRecords.Disclosures.Where(d => idSet.Contains(d.Id)))
            {
                if (!seen.Add(disclosure.Id))
                {
                    continue;
                }

                disclosures.Add(BuildSummary(disclosure, cveRecords, majorVersion, entry.Version, year, month));
                cveIds.Add(disclosure.Id);
            }
        }

        Directory.CreateDirectory(outputMajorVersionDir);

        await WriteCveIndexJsonAsync(majorVersion, cveIds, disclosures, lastUpdated, outputMajorVersionDir);
        await WriteCveMarkdownAsync(majorVersion, patchEntries, inputRoot, outputMajorVersionDir);
    }

    private static CveRecordSummary BuildSummary(
        Dotnet.Release.Cve.Cve disclosure,
        CveRecords cveRecords,
        string majorVersion,
        string patchVersion,
        string year,
        string month)
    {
        // `fixed` is the patch version that shipped the fix for this major. Prefer the
        // product entry's authoritative `fixed` when present, otherwise the patch version.
        var fixedVersion = cveRecords.Products?
            .FirstOrDefault(p => p.CveId == disclosure.Id && p.Release == majorVersion)?.Fixed
            ?? patchVersion;

        var links = new Dictionary<string, object>();

        var announcementUrl = disclosure.References?.FirstOrDefault();
        if (announcementUrl != null)
        {
            links[HalTerms.Self] = new HalLink(announcementUrl);
        }

        var monthIndexPath = $"{FileNames.Directories.Timeline}/{year}/{month}/{FileNames.Index}";
        links[LinkRelations.DisclosureMonth] = new HalLink($"{Location.GitHubBaseUri}{monthIndexPath}");

        var cveJsonPath = $"{FileNames.Directories.Timeline}/{year}/{month}/{FileNames.Cve}";
        links[LinkRelations.CveJson] = new HalLink($"{Location.GitHubBaseUri}{cveJsonPath}")
        {
            Type = MediaType.Json
        };

        var affectedReleases = cveRecords.CveReleases?.TryGetValue(disclosure.Id, out var releases) == true
            ? releases
            : null;

        // Slim: inline cheap high-signal fields only; leave affected_products /
        // affected_packages / platforms to the canonical month cve.json.
        return new CveRecordSummary(disclosure.Id, disclosure.Problem)
        {
            Fixed = fixedVersion,
            Links = links,
            CvssScore = disclosure.Cvss.Score,
            CvssSeverity = disclosure.Cvss.Severity,
            DisclosureDate = disclosure.Timeline.Disclosure.Date,
            AffectedReleases = affectedReleases,
            Aliases = disclosure.Aliases
        };
    }

    private static async Task WriteCveIndexJsonAsync(
        string majorVersion,
        IReadOnlyList<string> cveIds,
        IReadOnlyList<CveRecordSummary> disclosures,
        DateTimeOffset? lastUpdated,
        string outputMajorVersionDir)
    {
        var links = new Dictionary<string, HalLink>
        {
            [HalTerms.Self] = new HalLink($"{Location.GitHubBaseUri}{majorVersion}/{FileNames.CveIndex}")
            {
                Type = MediaType.Json
            },
            [LinkRelations.Major] = new HalLink($"{Location.GitHubBaseUri}{majorVersion}/{FileNames.Index}"),
            [LinkRelations.CveMarkdown] = new HalLink($"{Location.GitHubBaseUri}{majorVersion}/{FileNames.CveMarkdown}")
            {
                Type = MediaType.Markdown
            }
        };

        var index = new MajorCveIndex($".NET {majorVersion} CVE index", majorVersion)
        {
            LastUpdated = lastUpdated?.ToString("yyyy-MM-dd"),
            CveRecords = cveIds.Count > 0 ? cveIds.ToList() : null,
            Links = links,
            Embedded = disclosures.Count > 0
                ? new MajorCveIndexEmbedded { Disclosures = disclosures }
                : null
        };

        var json = JsonSerializer.Serialize(index, MajorCveIndexSerializerContext.Default.MajorCveIndex);
        var path = Path.Combine(outputMajorVersionDir, FileNames.CveIndex);
        await File.WriteAllTextAsync(path, json + '\n');
    }

    private static async Task WriteCveMarkdownAsync(
        string majorVersion,
        IReadOnlyList<ReleaseVersionIndexEntry> patchEntries,
        string inputRoot,
        string outputMajorVersionDir)
    {
        var majorNumber = majorVersion.Split('.')[0];
        var labelSlug = $".NET {majorVersion}".Replace(" ", "%20");

        var sb = new StringBuilder();
        sb.AppendLine($"# .NET {majorNumber} CVEs");
        sb.AppendLine();
        sb.AppendLine($"The .NET Team releases [monthly updates for .NET {majorNumber}](https://github.com/dotnet/announcements/labels/{labelSlug}) on [Patch Tuesday](https://en.wikipedia.org/wiki/Patch_Tuesday). These updates often include security fixes. If you are on an older version, your app may be vulnerable.");
        sb.AppendLine();
        sb.AppendLine($"Your app needs to be on the latest .NET {majorNumber} patch version to be secure. The longer you wait to upgrade, the greater the exposure to CVEs.");
        sb.AppendLine();
        sb.AppendLine("## Which CVEs apply to my app?");
        sb.AppendLine();
        sb.AppendLine("Your app may be vulnerable to the following published security [CVEs](https://www.cve.org/) if you are using an older version.");
        sb.AppendLine();

        var anyRows = false;
        foreach (var entry in patchEntries)
        {
            var gaDate = entry.Lifecycle?.GaDate;
            if (gaDate is null)
            {
                continue;
            }

            anyRows = true;
            var monthYear = gaDate.Value.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
            sb.AppendLine($"- {entry.Version} ({monthYear})");

            if (entry.CveRecords is null or { Count: 0 })
            {
                sb.AppendLine("  - No new CVEs.");
                continue;
            }

            var date = gaDate.Value;
            var cveRecords = await CveLoader.LoadForReleaseDateAsync(inputRoot, date);
            IEnumerable<string> monthIds = cveRecords?.ReleaseCves?.TryGetValue(majorVersion, out var ids) == true
                ? ids
                : entry.CveRecords;
            var idSet = monthIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var matched = cveRecords?.Disclosures?.Where(d => idSet.Contains(d.Id)).ToList();
            if (matched is null or { Count: 0 })
            {
                sb.AppendLine("  - No new CVEs.");
                continue;
            }

            foreach (var disclosure in matched)
            {
                var url = disclosure.References?.FirstOrDefault();
                var link = url != null
                    ? $"[{disclosure.Id} | {disclosure.Problem}]({url})"
                    : $"{disclosure.Id} | {disclosure.Problem}";
                sb.AppendLine($"  - {link}");
            }
        }

        if (!anyRows)
        {
            sb.AppendLine($"No CVEs have been published for .NET {majorNumber} yet. This page will be updated when .NET {majorNumber} reaches GA.");
        }

        var path = Path.Combine(outputMajorVersionDir, FileNames.CveMarkdown);
        await File.WriteAllTextAsync(path, sb.ToString());
    }
}
