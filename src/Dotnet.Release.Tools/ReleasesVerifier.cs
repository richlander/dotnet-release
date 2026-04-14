using System.Security.Cryptography;
using System.Text.Json;
using Dotnet.Release;
using Dotnet.Release.Graph;
using Dotnet.Release.Releases;
using Markout;

namespace Dotnet.Release.Tools;

/// <summary>
/// Verifies that all URLs in the latest patch release of each supported .NET version
/// return HTTP 200, and optionally validates file hashes.
/// </summary>
public static class ReleasesVerifier
{
    public static async Task<ReleasesVerificationReport> VerifyAsync(
        string releasesNotesPath, HttpClient client, TextWriter log, bool skipHash = false, string? filterVersion = null)
    {
        // Parse filter: "10.0" → channel only, "10.0.5" → specific patch
        string? filterChannel = null;
        string? filterPatch = null;

        if (filterVersion is not null)
        {
            // Channel version has one dot (e.g., "10.0"), patch has two+ (e.g., "10.0.5")
            int dotCount = filterVersion.Count(c => c == '.');
            if (dotCount == 1)
            {
                filterChannel = filterVersion;
            }
            else
            {
                // Extract channel from patch: "10.0.5" → "10.0"
                int secondDot = filterVersion.IndexOf('.', filterVersion.IndexOf('.') + 1);
                filterChannel = filterVersion[..secondDot];
                filterPatch = filterVersion;
            }
        }

        var versions = ReleasesIndexGenerator.DiscoverVersions(releasesNotesPath);
        var versionReports = new List<MajorVersionLinkReport>();

        foreach (var version in versions)
        {
            // Filter to specific channel version if requested
            if (filterChannel is not null && version != filterChannel) continue;

            var releasesPath = Path.Combine(releasesNotesPath, version, FileNames.Releases);
            if (!File.Exists(releasesPath)) continue;

            using var stream = File.OpenRead(releasesPath);
            var overview = await JsonSerializer.DeserializeAsync(stream, MajorReleaseOverviewSerializerContext.Default.MajorReleaseOverview);
            if (overview is null) continue;

            // Skip EOL versions unless a specific version was requested
            if (filterVersion is null && overview.SupportPhase == SupportPhase.Eol) continue;

            PatchRelease? targetPatch;
            string targetVersion;

            if (filterPatch is not null)
            {
                // Find the specific patch release
                targetPatch = overview.Releases.FirstOrDefault(r => r.ReleaseVersion == filterPatch);
                if (targetPatch is null)
                {
                    log.WriteLine($"Patch release {filterPatch} not found in {version}/releases.json");
                    return new ReleasesVerificationReport
                    {
                        GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"),
                        VersionsChecked = "none",
                        Versions = []
                    };
                }
                targetVersion = filterPatch;
            }
            else
            {
                targetPatch = overview.Releases.FirstOrDefault();
                if (targetPatch is null) continue;
                targetVersion = overview.LatestRelease;
            }

            log.WriteLine($"Checking .NET {version} ({targetVersion})...");
            var report = await VerifyPatchReleaseAsync(version, targetVersion, overview, targetPatch, releasesNotesPath, client, log, skipHash);
            versionReports.Add(report);
        }

        return new ReleasesVerificationReport
        {
            GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"),
            VersionsChecked = string.Join(", ", versionReports.Select(v => $".NET {v.Version}")),
            Versions = versionReports
        };
    }

    static async Task<MajorVersionLinkReport> VerifyPatchReleaseAsync(
        string version, string patchVersion, MajorReleaseOverview overview, PatchRelease release,
        string releasesNotesPath, HttpClient client, TextWriter log, bool skipHash)
    {
        var urls = CollectUrls(release);
        log.WriteLine($"  {urls.Count} URLs to check");

        // HTTP HEAD checks with concurrency limit
        var brokenLinks = new List<LinkIssue>();
        var semaphore = new SemaphoreSlim(16);

        var httpTasks = urls.Select(async u =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await CheckUrlAsync(u, client);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var httpResults = await Task.WhenAll(httpTasks);

        foreach (var result in httpResults)
        {
            if (!result.IsSuccess)
            {
                brokenLinks.Add(new LinkIssue(result.Url.Component, result.Url.FileName, result.Url.Url, result.StatusDescription));
                log.WriteLine($"  ❌ {result.Url.Component}/{result.Url.FileName}: {result.StatusDescription}");
            }
        }

        int successCount = httpResults.Count(r => r.IsSuccess);
        log.WriteLine($"  {successCount}/{urls.Count} URLs returned 200");

        // Hash verification for binary files
        var hashMismatches = new List<LinkIssue>();

        if (!skipHash)
        {
            var filesWithHashes = urls
                .Where(u => !string.IsNullOrEmpty(u.Hash))
                .Where(u => httpResults.First(r => r.Url == u).IsSuccess) // only check files that returned 200
                .ToList();

            if (filesWithHashes.Count > 0)
            {
                log.WriteLine($"  Verifying {filesWithHashes.Count} file hashes...");
                var hashSemaphore = new SemaphoreSlim(4);

                var hashTasks = filesWithHashes.Select(async u =>
                {
                    await hashSemaphore.WaitAsync();
                    try
                    {
                        return await VerifyHashAsync(u, client, log);
                    }
                    finally
                    {
                        hashSemaphore.Release();
                    }
                });

                var hashResults = await Task.WhenAll(hashTasks);

                foreach (var result in hashResults)
                {
                    if (!result.IsMatch)
                    {
                        string detail = result.ActualHash.StartsWith("Error:")
                            ? result.ActualHash
                            : $"Hash mismatch: expected {Truncate(result.ExpectedHash)}..., got {Truncate(result.ActualHash)}...";

                        hashMismatches.Add(new LinkIssue(result.Url.Component, result.Url.FileName, result.Url.Url, detail));
                        log.WriteLine($"  ❌ Hash mismatch: {result.Url.FileName}");
                    }
                }

                int hashSuccessCount = hashResults.Count(r => r.IsMatch);
                log.WriteLine($"  {hashSuccessCount}/{filesWithHashes.Count} hashes verified");
            }
        }

        // Verify latest.version files on CDN match releases.json
        log.WriteLine($"  Checking latest.version files on CDN...");
        var versionMismatches = await VerifyLatestVersionFilesAsync(version, overview, client, log);

        // Verify aka.ms redirect targets match releases.json URLs
        var akamsLinks = CollectAkamsLinks(release, overview, version, releasesNotesPath, log);
        var akamsMismatches = await VerifyAkamsLinksAsync(akamsLinks, log);

        // Build report
        var issues = new List<LinkIssueBucket>();

        if (brokenLinks.Count > 0)
        {
            issues.Add(new LinkIssueBucket
            {
                Alert = new Callout(CalloutSeverity.Warning, "Broken links (non-200 response)"),
                Items = brokenLinks
            });
        }

        if (hashMismatches.Count > 0)
        {
            issues.Add(new LinkIssueBucket
            {
                Alert = new Callout(CalloutSeverity.Warning, "Hash mismatches"),
                Items = hashMismatches
            });
        }

        if (versionMismatches.Count > 0)
        {
            issues.Add(new LinkIssueBucket
            {
                Alert = new Callout(CalloutSeverity.Important, "CDN latest.version files do not match releases.json"),
                Items = versionMismatches
            });
        }

        if (akamsMismatches.Count > 0)
        {
            issues.Add(new LinkIssueBucket
            {
                Alert = new Callout(CalloutSeverity.Warning, "aka.ms redirect mismatches"),
                Items = akamsMismatches
            });
        }

        return new MajorVersionLinkReport
        {
            Version = version,
            PatchVersion = patchVersion,
            UrlsChecked = urls.Count,
            Issues = issues
        };
    }

    static List<UrlInfo> CollectUrls(PatchRelease release)
    {
        var urls = new List<UrlInfo>();

        if (!string.IsNullOrEmpty(release.ReleaseNotes))
        {
            urls.Add(new UrlInfo("Release Notes", "release-notes", release.ReleaseNotes, null));
        }

        foreach (var cve in release.CveList)
        {
            if (!string.IsNullOrEmpty(cve.CveUrl))
            {
                urls.Add(new UrlInfo("CVE", cve.CveId, cve.CveUrl, null));
            }
        }

        AddComponentFiles(urls, "Runtime", release.Runtime.Files);
        AddComponentFiles(urls, "SDK", release.Sdk.Files);
        AddComponentFiles(urls, "ASP.NET Core", release.AspnetcoreRuntime.Files);

        if (release.WindowsDesktop is not null)
        {
            AddComponentFiles(urls, "Windows Desktop", release.WindowsDesktop.Files);
        }

        if (release.Symbols is not null)
        {
            AddComponentFiles(urls, "Symbols", release.Symbols.Files);
        }

        return urls;
    }

    static void AddComponentFiles(List<UrlInfo> urls, string component, IList<ComponentFile> files)
    {
        foreach (var file in files)
        {
            urls.Add(new UrlInfo(component, file.Name, file.Url, file.Hash));
        }
    }

    static async Task<HttpCheckResult> CheckUrlAsync(UrlInfo url, HttpClient client)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url.Url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.IsSuccessStatusCode)
            {
                return new HttpCheckResult(url, true, $"{(int)response.StatusCode}");
            }

            return new HttpCheckResult(url, false, $"{(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return new HttpCheckResult(url, false, $"Error: {ex.Message}");
        }
    }

    static async Task<HashCheckResult> VerifyHashAsync(UrlInfo url, HttpClient client, TextWriter log)
    {
        try
        {
            log.Write($"    Downloading {url.FileName}...");
            using var response = await client.GetAsync(url.Url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var sha512 = SHA512.Create();
            byte[] hash = await sha512.ComputeHashAsync(contentStream);
            string actualHash = Convert.ToHexStringLower(hash);
            string expectedHash = url.Hash!.ToLowerInvariant();
            bool match = actualHash == expectedHash;
            log.WriteLine(match ? " ✅" : " ❌");

            return new HashCheckResult(url, match, expectedHash, actualHash);
        }
        catch (Exception ex)
        {
            log.WriteLine($" Error: {ex.Message}");
            return new HashCheckResult(url, false, url.Hash ?? "", $"Error: {ex.Message}");
        }
    }

    static string Truncate(string s) => s.Length > 16 ? s[..16] : s;

    private const string CdnFeedBase = "https://builds.dotnet.microsoft.com/dotnet";

    /// <summary>
    /// Verifies that the dotnet-install.sh latest.version files on the CDN
    /// match the latest-sdk and latest-runtime values in releases.json.
    /// </summary>
    static async Task<List<LinkIssue>> VerifyLatestVersionFilesAsync(
        string channelVersion, MajorReleaseOverview overview, HttpClient client, TextWriter log)
    {
        var issues = new List<LinkIssue>();

        var checks = new (string Component, string UrlPath, string Expected)[]
        {
            ("SDK", $"{CdnFeedBase}/Sdk/{channelVersion}/latest.version", overview.LatestSdk),
            ("Runtime", $"{CdnFeedBase}/Runtime/{channelVersion}/latest.version", overview.LatestRuntime),
            ("ASP.NET Core Runtime", $"{CdnFeedBase}/aspnetcore/Runtime/{channelVersion}/latest.version",
                overview.Releases.FirstOrDefault()?.AspnetcoreRuntime.Version ?? ""),
        };

        foreach (var (component, url, expected) in checks)
        {
            if (string.IsNullOrEmpty(expected)) continue;

            try
            {
                string actual = (await client.GetStringAsync(url)).Trim();
                // latest.version may be multi-line (commit hash on first line, version on last)
                string actualVersion = actual.Contains('\n') ? actual.Split('\n')[^1].Trim() : actual;

                if (actualVersion == expected)
                {
                    log.WriteLine($"  ✅ {component} latest.version: {actualVersion}");
                }
                else
                {
                    log.WriteLine($"  ❌ {component} latest.version: CDN has {actualVersion}, releases.json has {expected}");
                    issues.Add(new LinkIssue(component, "latest.version", url,
                        $"CDN has {actualVersion}, releases.json has {expected}"));
                }
            }
            catch (Exception ex)
            {
                log.WriteLine($"  ❌ {component} latest.version: {ex.Message}");
                issues.Add(new LinkIssue(component, "latest.version", url, $"Error: {ex.Message}"));
            }
        }

        return issues;
    }

    /// <summary>
    /// Collects aka.ms links from ComponentFile.Akams in releases.json and from downloads/*.json files.
    /// Each link is paired with the expected concrete download URL from releases.json.
    /// </summary>
    static List<AkamsInfo> CollectAkamsLinks(
        PatchRelease release, MajorReleaseOverview overview, string version, string releasesNotesPath, TextWriter log)
    {
        var links = new List<AkamsInfo>();

        // 1. Collect aka.ms links from ComponentFile.Akams fields in releases.json
        AddAkamsFromComponent(links, "Runtime", release.Runtime.Files);
        AddAkamsFromComponent(links, "SDK", release.Sdk.Files);
        AddAkamsFromComponent(links, "ASP.NET Core", release.AspnetcoreRuntime.Files);
        if (release.WindowsDesktop is not null)
            AddAkamsFromComponent(links, "Windows Desktop", release.WindowsDesktop.Files);

        // 2. Collect aka.ms links from downloads/*.json (evergreen redirects)
        //    These should redirect to the matching file in releases.json
        var downloadsDir = Path.Combine(releasesNotesPath, version, "downloads");
        if (Directory.Exists(downloadsDir))
        {
            // Build lookup: file name → concrete URL from releases.json
            var urlByName = BuildFileUrlLookup(release);
            var sdkBandLookups = BuildSdkBandUrlLookups(overview);

            foreach (var jsonFile in Directory.GetFiles(downloadsDir, "*.json"))
            {
                if (Path.GetFileName(jsonFile) == "index.json") continue;
                AddAkamsFromDownloadsFile(links, jsonFile, urlByName, sdkBandLookups, log);
            }
        }

        return links;
    }

    static void AddAkamsFromComponent(List<AkamsInfo> links, string component, IList<ComponentFile> files)
    {
        foreach (var file in files)
        {
            if (!string.IsNullOrEmpty(file.Akams))
                links.Add(new AkamsInfo(component, file.Name, file.Akams, file.Url));
        }
    }

    /// <summary>
    /// Builds a lookup from file name to concrete download URL across all components in a patch release.
    /// </summary>
    static Dictionary<string, string> BuildFileUrlLookup(PatchRelease release)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddFilesToLookup(lookup, release.Runtime.Files);
        AddFilesToLookup(lookup, release.Sdk.Files);
        AddFilesToLookup(lookup, release.AspnetcoreRuntime.Files);
        if (release.WindowsDesktop is not null)
            AddFilesToLookup(lookup, release.WindowsDesktop.Files);
        if (release.Symbols is not null)
            AddFilesToLookup(lookup, release.Symbols.Files);
        return lookup;
    }

    static void AddFilesToLookup(Dictionary<string, string> lookup, IList<ComponentFile> files)
    {
        foreach (var file in files)
        {
            lookup.TryAdd(file.Name, file.Url);
        }
    }

    static Dictionary<string, Dictionary<string, string>> BuildSdkBandUrlLookups(MajorReleaseOverview overview)
    {
        var lookups = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var patch in overview.Releases)
        {
            if (patch.Sdks is null)
            {
                continue;
            }

            foreach (var sdk in patch.Sdks)
            {
                string band = GetSdkFeatureBand(sdk.Version);
                if (lookups.ContainsKey(band))
                {
                    continue;
                }

                var bandLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                AddFilesToLookup(bandLookup, sdk.Files);
                lookups.Add(band, bandLookup);
            }
        }

        return lookups;
    }

    static string GetSdkFeatureBand(string sdkVersion)
    {
        if (string.IsNullOrEmpty(sdkVersion) || sdkVersion.Length < 5)
        {
            return sdkVersion;
        }

        return $"{sdkVersion[..5]}xx";
    }

    /// <summary>
    /// Reads a downloads/*.json file and collects aka.ms → expected URL pairs.
    /// </summary>
    static void AddAkamsFromDownloadsFile(
        List<AkamsInfo> links,
        string jsonFile,
        Dictionary<string, string> urlByName,
        Dictionary<string, Dictionary<string, string>> sdkBandLookups,
        TextWriter log)
    {
        try
        {
            using var stream = File.OpenRead(jsonFile);
            var download = JsonSerializer.Deserialize(stream, DownloadsIndexSerializerContext.Default.ComponentDownload);
            if (download?.Embedded?.Downloads is null) return;

            string component = download.Component ?? Path.GetFileNameWithoutExtension(jsonFile);
            Dictionary<string, string>? expectedLookup = null;

            if (component.Equals("sdk", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(download.FeatureBand))
            {
                if (!sdkBandLookups.TryGetValue(download.FeatureBand, out expectedLookup))
                {
                    log.WriteLine($"  ⚠️ Could not find releases.json entries for SDK feature band {download.FeatureBand}.");
                }
            }

            expectedLookup ??= urlByName;

            foreach (var (_, file) in download.Embedded.Downloads)
            {
                if (file.Links is null) continue;

                if (file.Links.TryGetValue("download", out var downloadLink) &&
                    downloadLink.Href.Contains("aka.ms", StringComparison.OrdinalIgnoreCase))
                {
                    // Match the download file name to the concrete URL in releases.json
                    if (expectedLookup.TryGetValue(file.Name, out var expectedUrl))
                    {
                        links.Add(new AkamsInfo(component, file.Name, downloadLink.Href, expectedUrl));
                    }
                    else
                    {
                        // No matching file in releases.json — still check the redirect resolves
                        links.Add(new AkamsInfo(component, file.Name, downloadLink.Href, ""));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.WriteLine($"  ⚠️ Could not read {Path.GetFileName(jsonFile)}: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies that each aka.ms short link redirects to the expected download URL.
    /// </summary>
    static async Task<List<LinkIssue>> VerifyAkamsLinksAsync(
        List<AkamsInfo> links, TextWriter log)
    {
        if (links.Count == 0) return [];

        log.WriteLine($"  Checking {links.Count} aka.ms redirect(s)...");

        // Use a non-redirecting client to inspect the Location header
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var noRedirectClient = new HttpClient(handler);
        var issues = new List<LinkIssue>();
        var semaphore = new SemaphoreSlim(16);

        var tasks = links.Select(async link =>
        {
            await semaphore.WaitAsync();
            try
            {
                using var response = await noRedirectClient.GetAsync(link.AkamsUrl, HttpCompletionOption.ResponseHeadersRead);
                string? location = response.Headers.Location?.ToString();

                if (location is null)
                {
                    return (link, Issue: $"aka.ms returned {(int)response.StatusCode} with no redirect");
                }

                if (string.IsNullOrEmpty(link.ExpectedUrl))
                {
                    // No releases.json match — just note the redirect target
                    return (link, Issue: (string?)null);
                }

                if (location == link.ExpectedUrl)
                {
                    return (link, Issue: (string?)null);
                }

                return (link, Issue: $"Redirects to {location} but releases.json has {link.ExpectedUrl}");
            }
            catch (Exception ex)
            {
                return (link, Issue: $"Error: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        int successCount = 0;

        foreach (var (link, issue) in results)
        {
            if (issue is null)
            {
                successCount++;
            }
            else
            {
                log.WriteLine($"  ❌ {link.Component}/{link.FileName}: {issue}");
                issues.Add(new LinkIssue(link.Component, link.FileName, link.AkamsUrl, issue));
            }
        }

        log.WriteLine($"  {successCount}/{links.Count} aka.ms redirects verified");
        return issues;
    }

    // Internal types
    internal record UrlInfo(string Component, string FileName, string Url, string? Hash);
    record AkamsInfo(string Component, string FileName, string AkamsUrl, string ExpectedUrl);
    record HttpCheckResult(UrlInfo Url, bool IsSuccess, string StatusDescription);
    record HashCheckResult(UrlInfo Url, bool IsMatch, string ExpectedHash, string ActualHash);
}

// --- Markout report models ---

[MarkoutSerializable(TitleProperty = nameof(Title))]
public class ReleasesVerificationReport
{
    public string Title => "Release Link Verification";

    [MarkoutPropertyName("Generated")]
    public string GeneratedAt { get; set; } = "";

    [MarkoutPropertyName("Versions checked")]
    public string VersionsChecked { get; set; } = "";

    [MarkoutUnwrap]
    public List<MajorVersionLinkReport> Versions { get; set; } = [];

    [MarkoutIgnore]
    public bool HasIssues => Versions.Any(v => v.HasIssues);
}

[MarkoutSerializable(TitleProperty = nameof(Title))]
public class MajorVersionLinkReport
{
    [MarkoutIgnore]
    public string Version { get; set; } = "";

    [MarkoutIgnore]
    public string PatchVersion { get; set; } = "";

    public string Title => $".NET {Version} — {PatchVersion}";

    [MarkoutPropertyName("URLs checked")]
    public int UrlsChecked { get; set; }

    [MarkoutUnwrap]
    public List<LinkIssueBucket> Issues { get; set; } = [];

    [MarkoutIgnore]
    public bool HasIssues => Issues.Count > 0;
}

[MarkoutSerializable]
public class LinkIssueBucket
{
    [MarkoutIgnoreInTable]
    public required Callout Alert { get; init; }

    [MarkoutSection(Name = "")]
    public List<LinkIssue> Items { get; init; } = [];
}

[MarkoutSerializable]
public record LinkIssue(
    string Component,
    string File,
    [property: MarkoutPropertyName("URL")] string Url,
    string Status);

[MarkoutContext(typeof(ReleasesVerificationReport))]
[MarkoutContext(typeof(LinkIssue))]
[MarkoutContextOptions(SuppressTableWarnings = true)]
public partial class ReleasesVerificationReportContext : MarkoutSerializerContext { }
