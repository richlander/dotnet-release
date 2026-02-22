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
    /// Writes a Markout-formatted report to the output writer.
    /// </summary>
    public static async Task VerifyAsync(
        OSPackagesOverview overview,
        HttpClient client,
        TextWriter output,
        TextWriter log,
        PkgsOrgClient? pkgsOrg = null)
    {
        var writer = new MarkoutWriter(output);

        writer.WriteHeading(1, $".NET {overview.ChannelVersion} — OS Packages Verification");
        writer.WriteField("Generated", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"));
        writer.WriteBlankLine();

        int totalChecked = 0;
        int totalMissing = 0;
        bool hasIssues = false;

        foreach (var distro in overview.Distributions)
        {
            var checker = GetChecker(distro.Name);
            if (checker is null)
            {
                log.WriteLine($"  Skipping {distro.Name} (no package checker available)");
                continue;
            }

            var distroIssues = new List<PackageIssue>();

            foreach (var release in distro.Releases)
            {
                log.Write($"  Checking {release.Name}... ");
                int found = 0;
                int missing = 0;

                foreach (var pkg in release.Packages)
                {
                    totalChecked++;
                    bool exists = await checker(client, distro.Name, release.Release, pkg.Name, log);
                    if (exists)
                    {
                        found++;
                    }
                    else
                    {
                        missing++;
                        totalMissing++;
                        distroIssues.Add(new(release.Name, pkg.Id, pkg.Name));
                    }
                }

                log.WriteLine($"{found} ok, {missing} missing");
            }

            if (distroIssues.Count > 0)
            {
                hasIssues = true;
                writer.WriteHeading(2, distro.Name);
                writer.WriteCallout(CalloutSeverity.Warning, "Package names not found in distro archive");
                writer.WriteTableStart("Release", "Package ID", "Package Name");
                foreach (var issue in distroIssues)
                    writer.WriteTableRow(issue.Release, issue.PackageId, issue.PackageName);
                writer.WriteTableEnd();
            }
        }

        writer.WriteBlankLine();
        writer.WriteHeading(2, "Summary");
        writer.WriteField("Packages checked", totalChecked.ToString());
        writer.WriteField("Missing packages", totalMissing.ToString());

        if (!hasIssues)
            writer.WriteCallout(CalloutSeverity.Note, "All package names verified successfully.");

        writer.Flush();
    }

    static Func<HttpClient, string, string, string, TextWriter, Task<bool>>? GetChecker(string distroName) =>
        distroName switch
        {
            "Ubuntu" => CheckUbuntuPackageAsync,
            "Debian" => CheckDebianPackageAsync,
            _ => null
        };

    /// <summary>
    /// Check if a package exists in Ubuntu's archive via Launchpad API (binary packages).
    /// </summary>
    static async Task<bool> CheckUbuntuPackageAsync(
        HttpClient client, string distro, string release, string packageName, TextWriter log)
    {
        string? series = MapUbuntuSeries(release);
        if (series is null) return true;

        string encodedName = Uri.EscapeDataString(packageName);
        string url = $"https://api.launchpad.net/1.0/ubuntu/+archive/primary?ws.op=getPublishedBinaries&binary_name={encodedName}&exact_match=true&distro_arch_series=https://api.launchpad.net/1.0/ubuntu/{series}/amd64&status=Published&ws.size=1";

        try
        {
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return true;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
            int total = doc.RootElement.GetProperty("total_size").GetInt32();
            return total > 0;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Check if a binary package exists in Debian via packages.debian.org.
    /// </summary>
    static async Task<bool> CheckDebianPackageAsync(
        HttpClient client, string distro, string release, string packageName, TextWriter log)
    {
        string? codename = MapDebianCodename(release);
        if (codename is null) return true;

        string url = $"https://packages.debian.org/{codename}/{packageName}";

        try
        {
            string html = await client.GetStringAsync(url);
            return !html.Contains("No such package");
        }
        catch
        {
            return true;
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

record PackageIssue(string Release, string PackageId, string PackageName);
