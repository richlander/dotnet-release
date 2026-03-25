using Dotnet.Release.Cve;
using Dotnet.Release.Graph;

namespace Dotnet.Release.CveHandler;

/// <summary>
/// Transforms CVE data between different formats (full disclosures to summaries).
/// </summary>
public static class CveTransformer
{
    /// <summary>
    /// Converts full CVE disclosure records to summary format for embedding in indexes.
    /// </summary>
    public static List<CveRecordSummary> ToSummaries(CveRecords cveRecords)
    {
        if (cveRecords.Disclosures is null or { Count: 0 })
        {
            return [];
        }

        return cveRecords.Disclosures.Select(disclosure =>
        {
            var affectedProducts = cveRecords.ProductCves?
                .Where(kv => kv.Value.Contains(disclosure.Id))
                .Select(kv => kv.Key)
                .ToList();

            var affectedPackages = cveRecords.PackageCves?
                .Where(kv => kv.Value.Contains(disclosure.Id))
                .Select(kv => kv.Key)
                .ToList();

            var links = new Dictionary<string, object>();
            var announcementUrl = disclosure.References?.FirstOrDefault();
            if (announcementUrl != null)
            {
                links["self"] = new HalLink(announcementUrl);
            }

            return new CveRecordSummary(disclosure.Id, disclosure.Problem)
            {
                Links = links.Count > 0 ? links : null,
                CvssScore = disclosure.Cvss.Score,
                CvssSeverity = disclosure.Cvss.Severity,
                DisclosureDate = disclosure.Timeline.Disclosure.Date,
                AffectedReleases = cveRecords.CveReleases?.TryGetValue(disclosure.Id, out var releases) == true
                    ? releases
                    : null,
                AffectedProducts = affectedProducts?.Count > 0 ? affectedProducts : null,
                AffectedPackages = affectedPackages?.Count > 0 ? affectedPackages : null,
                Platforms = disclosure.Platforms
            };
        }).ToList();
    }

    /// <summary>
    /// Extracts just the CVE IDs from CVE records.
    /// </summary>
    public static List<string> ExtractCveIds(CveRecords? cveRecords)
    {
        if (cveRecords?.Disclosures is null or { Count: 0 })
        {
            return [];
        }

        return cveRecords.Disclosures.Select(d => d.Id).ToList();
    }

    /// <summary>
    /// Filters CVE records to only include those affecting a specific release version.
    /// </summary>
    public static CveRecords? FilterByRelease(CveRecords? cveRecords, string releaseVersion)
    {
        if (cveRecords is null or { Disclosures.Count: 0 })
        {
            return null;
        }

        var cveIds = cveRecords.ReleaseCves?.TryGetValue(releaseVersion, out var ids) == true
            ? ids.ToHashSet()
            : new HashSet<string>();

        if (cveIds.Count == 0)
        {
            return null;
        }

        var filteredDisclosures = cveRecords.Disclosures
            .Where(d => cveIds.Contains(d.Id))
            .ToList();

        if (filteredDisclosures.Count == 0)
        {
            return null;
        }

        var filteredProducts = cveRecords.Products
            .Where(p => cveIds.Contains(p.CveId) && p.Release == releaseVersion)
            .ToList();

        var filteredPackages = cveRecords.Packages
            .Where(p => cveIds.Contains(p.CveId) && p.Release == releaseVersion)
            .ToList();

        var neededCommitHashes = filteredProducts.SelectMany(p => p.Commits)
            .Concat(filteredPackages.SelectMany(p => p.Commits))
            .ToHashSet();

        var filteredCommits = cveRecords.Commits?
            .Where(kv => neededCommitHashes.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var filteredProductCves = filteredProducts
            .GroupBy(p => p.Name)
            .ToDictionary(
                g => g.Key,
                g => (IList<string>)g.Select(p => p.CveId).Distinct().ToList()
            );

        var filteredPackageCves = filteredPackages
            .GroupBy(p => p.Name)
            .ToDictionary(
                g => g.Key,
                g => (IList<string>)g.Select(p => p.CveId).Distinct().ToList()
            );

        var filteredCveCommits = cveRecords.CveCommits?
            .Where(kv => cveIds.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var filteredSeverityCves = CveDictionaryGenerator.GenerateSeverityCves(filteredDisclosures);

        return new CveRecords(
            LastUpdated: cveRecords.LastUpdated,
            Title: $"CVEs affecting {releaseVersion}",
            Disclosures: filteredDisclosures,
            Products: filteredProducts,
            Packages: filteredPackages,
            Commits: filteredCommits,
            ProductName: cveRecords.ProductName,
            ProductCves: filteredProductCves,
            PackageCves: filteredPackageCves,
            ReleaseCves: new Dictionary<string, IList<string>> { [releaseVersion] = [.. cveIds] },
            SeverityCves: filteredSeverityCves,
            CveReleases: cveRecords.CveReleases?.Where(kv => cveIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            CveCommits: filteredCveCommits
        );
    }

    /// <summary>
    /// Validates that CVE records match what's in a release (from releases.json).
    /// Returns a list of validation messages.
    /// </summary>
    public static List<string> ValidateCveData(string releaseVersion, IReadOnlyList<string>? cveIdsFromRelease, IReadOnlyList<string>? cveIdsFromCveJson)
    {
        var messages = new List<string>();
        var releaseCves = cveIdsFromRelease?.ToHashSet() ?? [];
        var cveJsonCves = cveIdsFromCveJson?.ToHashSet() ?? [];

        if (releaseCves.Count == 0 && cveJsonCves.Count == 0)
        {
            return messages;
        }

        var inReleaseOnly = releaseCves.Except(cveJsonCves).ToList();
        var inCveJsonOnly = cveJsonCves.Except(releaseCves).ToList();

        if (inReleaseOnly.Count > 0)
        {
            messages.Add($"{releaseVersion}: CVE IDs in releases.json but not in cve.json: {string.Join(", ", inReleaseOnly)}");
        }

        if (inCveJsonOnly.Count > 0)
        {
            messages.Add($"{releaseVersion}: CVE IDs in cve.json but not in releases.json: {string.Join(", ", inCveJsonOnly)}");
        }

        return messages;
    }
}
