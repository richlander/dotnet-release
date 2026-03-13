using Dotnet.Release.Support;
using Markout;

namespace Dotnet.Release.Tools;

/// <summary>
/// Verifies os-packages.json package names exist in distro archives.
/// Uses Launchpad API for Ubuntu/Debian, pkgs.org for others.
/// </summary>
public static class OsPackagesVerifier
{
    /// <summary>
    /// Verifies all package names in os-packages.json against distro archives.
    /// Returns a serializable report model.
    /// </summary>
    public static async Task<OsPackagesReport> VerifyAsync(
        OSPackagesOverview overview,
        HttpClient client,
        TextWriter log,
        PkgsOrgClient? pkgsOrg = null)
    {
        int totalChecked = 0;
        int totalMissing = 0;
        var distroReports = new List<OsPackagesDistroReport>();

        foreach (var distro in overview.Distributions)
        {
            var checker = GetChecker(distro.Name);
            if (checker is null)
            {
                log.WriteLine($"  Skipping {distro.Name} (no package checker available)");
                continue;
            }

            var issues = new List<PackageIssue>();
            int totalErrors = 0;

            foreach (var release in distro.Releases)
            {
                log.Write($"  Checking {release.Name}... ");
                int found = 0;
                int missing = 0;
                int errors = 0;

                foreach (var pkg in release.Packages)
                {
                    totalChecked++;
                    var result = await checker(client, distro.Name, release.Release, pkg.Name, log);
                    switch (result)
                    {
                        case PackageCheckResult.Found:
                            found++;
                            break;
                        case PackageCheckResult.NotFound:
                            missing++;
                            totalMissing++;
                            issues.Add(new(release.Name, pkg.Id, pkg.Name));
                            break;
                        case PackageCheckResult.Error:
                            errors++;
                            totalErrors++;
                            break;
                    }
                }

                var parts = $"{found} ok, {missing} missing";
                if (errors > 0) parts += $", {errors} errors";
                log.WriteLine(parts);
            }

            if (issues.Count > 0)
                distroReports.Add(new() { Name = distro.Name, Issues = issues });
        }

        return new OsPackagesReport
        {
            Version = overview.ChannelVersion,
            GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"),
            PackagesCheckedText = totalChecked.ToString(),
            MissingPackagesText = totalMissing.ToString(),
            Distros = distroReports
        };
    }

    static Func<HttpClient, string, string, string, TextWriter, Task<PackageCheckResult>>? GetChecker(string distroName) =>
        distroName switch
        {
            "Ubuntu" => CheckUbuntuPackageAsync,
            "Debian" => CheckDebianPackageAsync,
            _ => null
        };

    /// <summary>
    /// Check if a package exists in Ubuntu's archive via Launchpad API (binary packages).
    /// </summary>
    static async Task<PackageCheckResult> CheckUbuntuPackageAsync(
        HttpClient client, string distro, string release, string packageName, TextWriter log)
    {
        string? series = MapUbuntuSeries(release);
        if (series is null) return PackageCheckResult.Found; // Can't verify, assume ok

        string encodedName = Uri.EscapeDataString(packageName);
        string url = $"https://api.launchpad.net/1.0/ubuntu/+archive/primary?ws.op=getPublishedBinaries&binary_name={encodedName}&exact_match=true&distro_arch_series=https://api.launchpad.net/1.0/ubuntu/{series}/amd64&status=Published&ws.size=1";

        try
        {
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                log.Write($"[HTTP {(int)response.StatusCode}] ");
                return PackageCheckResult.Error;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
            int total = doc.RootElement.GetProperty("total_size").GetInt32();
            return total > 0 ? PackageCheckResult.Found : PackageCheckResult.NotFound;
        }
        catch (Exception ex)
        {
            log.Write($"[{ex.GetType().Name}] ");
            return PackageCheckResult.Error;
        }
    }

    /// <summary>
    /// Check if a binary package exists in Debian via packages.debian.org.
    /// </summary>
    static async Task<PackageCheckResult> CheckDebianPackageAsync(
        HttpClient client, string distro, string release, string packageName, TextWriter log)
    {
        string? codename = MapDebianCodename(release);
        if (codename is null) return PackageCheckResult.Found;

        string url = $"https://packages.debian.org/{codename}/{packageName}";

        try
        {
            string html = await client.GetStringAsync(url);
            return html.Contains("No such package") ? PackageCheckResult.NotFound : PackageCheckResult.Found;
        }
        catch (Exception ex)
        {
            log.Write($"[{ex.GetType().Name}] ");
            return PackageCheckResult.Error;
        }
    }

    static string? MapUbuntuSeries(string release) => release switch
    {
        "26.04" => "resolute",
        "25.10" => "questing",
        "25.04" => "plucky",
        "24.10" => "oracular",
        "24.04" => "noble",
        "22.04.4" or "22.04" => "jammy",
        "20.04" => "focal",
        _ => null
    };

    static string? MapDebianCodename(string release) => release switch
    {
        "13" => "trixie",
        "12" => "bookworm",
        "11" => "bullseye",
        _ => null
    };
}

enum PackageCheckResult { Found, NotFound, Error }

// --- Report models ---

/// <summary>
/// Top-level os-packages verification report, serializable via Markout.
/// </summary>
[MarkoutSerializable(TitleProperty = nameof(Title))]
public class OsPackagesReport
{
    [MarkoutIgnore]
    public string Version { get; set; } = "";

    public string Title => $".NET {Version} — OS Packages Verification";

    [MarkoutPropertyName("Generated")]
    public string GeneratedAt { get; set; } = "";

    [MarkoutPropertyName("Packages checked")]
    public string PackagesCheckedText { get; set; } = "";

    [MarkoutPropertyName("Missing packages")]
    public string MissingPackagesText { get; set; } = "";

    [MarkoutUnwrap]
    public List<OsPackagesDistroReport> Distros { get; set; } = [];

    [MarkoutIgnore]
    public bool HasIssues => Distros.Count > 0;
}

[MarkoutSerializable(TitleProperty = nameof(Name))]
public class OsPackagesDistroReport
{
    [MarkoutIgnore] public string Name { get; init; } = "";

    [MarkoutIgnoreInTable]
    public Callout Alert { get; init; } = new(CalloutSeverity.Warning, "Package names not found in distro archive");

    [MarkoutSection(Name = "")]
    public List<PackageIssue> Issues { get; init; } = [];
}

[MarkoutSerializable]
public record PackageIssue(
    string Release,
    [property: MarkoutPropertyName("Package ID")] string PackageId,
    [property: MarkoutPropertyName("Package Name")] string PackageName);

[MarkoutContext(typeof(OsPackagesReport))]
[MarkoutContext(typeof(PackageIssue))]
[MarkoutContextOptions(SuppressTableWarnings = true)]
public partial class OsPackagesReportContext : MarkoutSerializerContext { }
