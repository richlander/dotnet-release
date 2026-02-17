using Dotnet.Release.Cve;

namespace Dotnet.Release.Client;

/// <summary>
/// Helper methods for filtering CVE records.
/// </summary>
public static class CveFilter
{
    /// <summary>
    /// Filters CVEs to only those affecting a specific .NET version.
    /// </summary>
    public static IEnumerable<Dotnet.Release.Cve.Cve> FilterByVersion(IEnumerable<CveRecords> cveRecords, string version)
    {
        ArgumentNullException.ThrowIfNull(cveRecords);
        ArgumentNullException.ThrowIfNullOrEmpty(version);

        var affectedCveIds = new HashSet<string>();

        foreach (var records in cveRecords)
        {
            foreach (var product in records.Products.Where(p => p.Release == version))
            {
                affectedCveIds.Add(product.CveId);
            }
        }

        return cveRecords
            .SelectMany(r => r.Disclosures)
            .Where(c => affectedCveIds.Contains(c.Id))
            .DistinctBy(c => c.Id);
    }

    /// <summary>
    /// Filters CVEs to only those affecting a specific platform.
    /// </summary>
    public static IEnumerable<Dotnet.Release.Cve.Cve> FilterByPlatform(IEnumerable<Dotnet.Release.Cve.Cve> cves, string platform, bool includeAll = true)
    {
        ArgumentNullException.ThrowIfNull(cves);
        ArgumentNullException.ThrowIfNullOrEmpty(platform);

        return cves.Where(c =>
            c.Platforms.Any(p => p.Equals(platform, StringComparison.OrdinalIgnoreCase)) ||
            (includeAll && c.Platforms.Any(p => p.Equals("all", StringComparison.OrdinalIgnoreCase))));
    }

    /// <summary>
    /// Filters CVEs to only those affecting specific platforms.
    /// </summary>
    public static IEnumerable<Dotnet.Release.Cve.Cve> FilterByPlatforms(IEnumerable<Dotnet.Release.Cve.Cve> cves, IEnumerable<string> platforms, bool includeAll = true)
    {
        ArgumentNullException.ThrowIfNull(cves);
        ArgumentNullException.ThrowIfNull(platforms);

        var platformSet = new HashSet<string>(platforms, StringComparer.OrdinalIgnoreCase);

        return cves.Where(c =>
            c.Platforms.Any(p => platformSet.Contains(p)) ||
            (includeAll && c.Platforms.Any(p => p.Equals("all", StringComparison.OrdinalIgnoreCase))));
    }

    /// <summary>
    /// Filters CVEs by severity level.
    /// </summary>
    public static IEnumerable<Dotnet.Release.Cve.Cve> FilterBySeverity(IEnumerable<Dotnet.Release.Cve.Cve> cves, string severity)
    {
        ArgumentNullException.ThrowIfNull(cves);
        ArgumentNullException.ThrowIfNullOrEmpty(severity);

        return cves.Where(c => c.Cvss.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Filters CVEs to only Critical and High severity.
    /// </summary>
    public static IEnumerable<Dotnet.Release.Cve.Cve> FilterHighSeverity(IEnumerable<Dotnet.Release.Cve.Cve> cves)
    {
        ArgumentNullException.ThrowIfNull(cves);
        return cves.Where(c =>
            c.Cvss.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase) ||
            c.Cvss.Severity.Equals("High", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Combines filtering by version and platform.
    /// </summary>
    public static IEnumerable<Dotnet.Release.Cve.Cve> FilterByVersionAndPlatform(
        IEnumerable<CveRecords> cveRecords, string version, string platform, bool includeAllPlatforms = true)
    {
        var versionFiltered = FilterByVersion(cveRecords, version);
        return FilterByPlatform(versionFiltered, platform, includeAllPlatforms);
    }
}
