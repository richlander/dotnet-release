using System.Text.Json;
using Dotnet.Release.Support;

namespace Dotnet.Release.Tools;

/// <summary>
/// Generates distro-packages.json by querying pkgs.org + supplemental feeds.
/// </summary>
public static class DistroPackagesGenerator
{
    public static readonly DotnetComponent[] Components =
    [
        new("sdk", ".NET SDK"),
        new("runtime", ".NET Runtime"),
        new("aspnetcore-runtime", "ASP.NET Core Runtime"),
    ];

    /// <summary>
    /// Queries all data sources and produces a DistroPackagesOverview for the given .NET version.
    /// </summary>
    public static async Task<DistroPackagesOverview> QueryAsync(
        string dotnetVersion,
        PkgsOrgClient pkgsOrg,
        HttpClient http,
        TextWriter log)
    {
        string[] parts = dotnetVersion.Split('.');
        string major = parts[0];
        string minor = parts.Length > 1 ? parts[1] : "0";

        // Step 1: Get distro metadata from pkgs.org
        log.WriteLine("Fetching pkgs.org distributions...");
        var pkgsDistros = await pkgsOrg.GetDistributionsAsync();
        log.WriteLine($"  {pkgsDistros.Count} distributions");

        var pkgsRepos = await pkgsOrg.GetRepositoriesAsync();
        log.WriteLine($"  {pkgsRepos.Count} repositories");

        // Build lookup maps
        var distroById = pkgsDistros.ToDictionary(d => d.Id);
        var repoById = pkgsRepos.ToDictionary(r => r.Id);

        // Step 2: Search pkgs.org for .NET packages
        log.WriteLine($"\nSearching pkgs.org for .NET {dotnetVersion} packages...");
        var pkgsResults = await SearchPkgsOrgAsync(pkgsOrg, major, minor, log);
        log.WriteLine($"  {pkgsResults.Count} total package results");

        // Step 3: Group results by distro
        var pkgsGrouped = GroupByDistro(pkgsResults, distroById, repoById);

        // Step 4: Load distro-sources.json for supplemental feeds + metadata
        var distroSources = DistroSource.Load();

        // Step 5: Check supplemental feeds (backports PPA, Homebrew, NixOS)
        log.WriteLine("\nChecking supplemental feeds...");
        var supplemental = await CheckSupplementalFeedsAsync(distroSources, dotnetVersion, major, minor, http, log);

        // Step 6: Merge pkgs.org results + supplemental into output model
        log.WriteLine("\nBuilding output...");
        var distributions = MergeResults(pkgsGrouped, supplemental, distroSources);

        return new DistroPackagesOverview(
            dotnetVersion,
            DateOnly.FromDateTime(DateTime.UtcNow),
            [.. Components],
            distributions);
    }

    static async Task<IList<PkgsOrgPackage>> SearchPkgsOrgAsync(
        PkgsOrgClient pkgsOrg, string major, string minor, TextWriter log)
    {
        var allResults = new List<PkgsOrgPackage>();

        // Search for common naming patterns across all distros
        string[] queries =
        [
            $"dotnet-sdk-{major}.{minor}",
            $"dotnet-runtime-{major}.{minor}",
            $"aspnetcore-runtime-{major}.{minor}",
            $"dotnet{major}-sdk",        // Alpine naming
            $"dotnet{major}-runtime",    // Alpine naming
            $"dotnet-sdk",               // Arch (unversioned)
            $"dotnet-runtime",           // Arch (unversioned)
            $"dotnet-{major}-sdk",       // Wolfi naming
            $"dotnet-{major}-runtime",   // Wolfi naming
        ];

        foreach (var query in queries)
        {
            log.Write($"  Searching '{query}'... ");
            var results = await pkgsOrg.SearchAsync(query, official: true, architecture: "intel");
            log.WriteLine($"{results.Count} results");
            allResults.AddRange(results);
            await Task.Delay(300); // Rate limiting
        }

        return allResults;
    }

    /// <summary>
    /// Groups pkgs.org results by "DistroName Version" → feed → packages.
    /// </summary>
    static Dictionary<string, Dictionary<string, List<DotnetDistroPackage>>> GroupByDistro(
        IList<PkgsOrgPackage> packages,
        Dictionary<int, PkgsOrgDistribution> distroById,
        Dictionary<int, PkgsOrgRepository> repoById)
    {
        var grouped = new Dictionary<string, Dictionary<string, List<DotnetDistroPackage>>>();

        foreach (var pkg in packages)
        {
            if (!distroById.TryGetValue(pkg.DistributionId, out var distro))
                continue;
            if (!repoById.TryGetValue(pkg.RepositoryId, out var repo))
                continue;

            string distroKey = $"{distro.Name} {distro.Version}";
            string? componentId = InferComponentId(pkg.Name);
            if (componentId is null) continue;

            if (!grouped.TryGetValue(distroKey, out var feeds))
            {
                feeds = [];
                grouped[distroKey] = feeds;
            }

            string feedName = repo.Official ? "builtin" : "thirdparty";
            if (!feeds.TryGetValue(feedName, out var feedPackages))
            {
                feedPackages = [];
                feeds[feedName] = feedPackages;
            }

            // Avoid duplicates
            if (feedPackages.Any(p => p.ComponentId == componentId)) continue;

            feedPackages.Add(new DotnetDistroPackage(
                componentId,
                pkg.Name,
                pkg.Version,
                [pkg.Architecture],
                repo.Name));
        }

        return grouped;
    }

    /// <summary>
    /// Infers the .NET component ID from a package name.
    /// </summary>
    static string? InferComponentId(string packageName)
    {
        if (packageName.Contains("aspnetcore") || packageName.Contains("aspnet"))
            return "aspnetcore-runtime";
        if (packageName.Contains("sdk"))
            return "sdk";
        if (packageName.Contains("runtime"))
            return "runtime";
        return null;
    }

    /// <summary>
    /// Checks supplemental feeds not covered by pkgs.org.
    /// </summary>
    static async Task<Dictionary<string, Dictionary<string, List<DotnetDistroPackage>>>> CheckSupplementalFeedsAsync(
        DistroSourceCollection distroSources,
        string dotnetVersion,
        string major,
        string minor,
        HttpClient http,
        TextWriter log)
    {
        var results = new Dictionary<string, Dictionary<string, List<DotnetDistroPackage>>>();

        foreach (var source in distroSources.Sources)
        {
            if (source.DotnetFeeds is null) continue;

            foreach (var (feedName, feed) in source.DotnetFeeds)
            {
                switch (feed.Type)
                {
                    case "launchpad":
                        await CheckLaunchpadFeedAsync(source, feedName, feed, major, minor, http, log, results);
                        break;
                    case "brew_formula":
                        await CheckBrewFormulaAsync(source, feedName, feed, major, minor, http, log, results);
                        break;
                    case "nixpkgs_github":
                        await CheckNixpkgsAsync(source, feedName, feed, major, minor, http, log, results);
                        break;
                }
            }
        }

        return results;
    }

    static async Task CheckLaunchpadFeedAsync(
        DistroSource source, string feedName, DotnetFeed feed,
        string major, string minor, HttpClient http, TextWriter log,
        Dictionary<string, Dictionary<string, List<DotnetDistroPackage>>> results)
    {
        if (source.Codenames is null) return;

        foreach (var (version, codename) in source.Codenames)
        {
            string url = DistroSource.ResolveFeedUrl(feed.Url, version, codename, major, minor);
            string distroKey = $"{source.Name} {version}";

            log.Write($"  [{feedName}] {distroKey}... ");
            try
            {
                using var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode) { log.WriteLine("not found"); continue; }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);
                int totalSize = json.RootElement.GetProperty("total_size").GetInt32();

                if (totalSize > 0)
                {
                    log.WriteLine($"FOUND ({totalSize} entries)");
                    string pattern = source.DotnetPackagePattern ?? "dotnet-{component}-{major}.{minor}";

                    if (!results.TryGetValue(distroKey, out var feeds))
                    {
                        feeds = [];
                        results[distroKey] = feeds;
                    }

                    var packages = new List<DotnetDistroPackage>();
                    foreach (var component in Components)
                    {
                        string pkgName = pattern
                            .Replace("{component}", component.Id)
                            .Replace("{major}", major)
                            .Replace("{minor}", minor);
                        packages.Add(new DotnetDistroPackage(component.Id, pkgName));
                    }
                    feeds[feedName] = packages;
                }
                else
                {
                    log.WriteLine("empty");
                }
            }
            catch (Exception ex)
            {
                log.WriteLine($"ERROR: {ex.Message}");
            }

            await Task.Delay(200);
        }
    }

    static async Task CheckBrewFormulaAsync(
        DistroSource source, string feedName, DotnetFeed feed,
        string major, string minor, HttpClient http, TextWriter log,
        Dictionary<string, Dictionary<string, List<DotnetDistroPackage>>> results)
    {
        string url = DistroSource.ResolveFeedUrl(feed.Url, null, null, major, minor);
        string distroKey = source.Name;

        log.Write($"  [{feedName}] {distroKey} dotnet@{major}... ");
        try
        {
            using var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode) { log.WriteLine("not found"); return; }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            string version = json.RootElement.GetProperty("versions").GetProperty("stable").GetString() ?? "unknown";
            log.WriteLine($"FOUND (v{version})");

            if (!results.TryGetValue(distroKey, out var feeds))
            {
                feeds = [];
                results[distroKey] = feeds;
            }

            // Homebrew formula provides SDK + runtime in one package
            feeds[feedName] =
            [
                new DotnetDistroPackage("sdk", $"dotnet@{major}", version),
                new DotnetDistroPackage("runtime", $"dotnet@{major}", version),
                new DotnetDistroPackage("aspnetcore-runtime", $"dotnet@{major}", version),
            ];
        }
        catch (Exception ex)
        {
            log.WriteLine($"ERROR: {ex.Message}");
        }
    }

    static async Task CheckNixpkgsAsync(
        DistroSource source, string feedName, DotnetFeed feed,
        string major, string minor, HttpClient http, TextWriter log,
        Dictionary<string, Dictionary<string, List<DotnetDistroPackage>>> results)
    {
        // Get active NixOS versions from endoflife.date
        var nixVersions = new List<string>();
        try
        {
            using var eolResponse = await http.GetAsync("https://endoflife.date/api/nixos.json");
            eolResponse.EnsureSuccessStatusCode();
            using var eolStream = await eolResponse.Content.ReadAsStreamAsync();
            using var cycles = await JsonDocument.ParseAsync(eolStream);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            foreach (var cycle in cycles.RootElement.EnumerateArray())
            {
                var eolStr = cycle.GetProperty("eol").GetString();
                if (eolStr is not null && DateOnly.TryParse(eolStr, out var eol) && eol > today)
                {
                    nixVersions.Add(cycle.GetProperty("cycle").GetString()!);
                }
            }
        }
        catch (Exception ex)
        {
            log.WriteLine($"  [nixpkgs] Failed to get NixOS versions: {ex.Message}");
            return;
        }

        foreach (var version in nixVersions)
        {
            string url = DistroSource.ResolveFeedUrl(feed.Url, version, null, major, minor);
            string distroKey = $"NixOS {version}";

            log.Write($"  [{feedName}] {distroKey} dotnet{major}... ");
            try
            {
                using var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode) { log.WriteLine("not found"); continue; }

                log.WriteLine("FOUND");

                if (!results.TryGetValue(distroKey, out var feeds))
                {
                    feeds = [];
                    results[distroKey] = feeds;
                }

                feeds[feedName] =
                [
                    new DotnetDistroPackage("sdk", $"dotnet-sdk_{major}", null),
                    new DotnetDistroPackage("runtime", $"dotnet-runtime_{major}", null),
                    new DotnetDistroPackage("aspnetcore-runtime", $"dotnet-aspnetcore_{major}", null),
                ];
            }
            catch (Exception ex)
            {
                log.WriteLine($"ERROR: {ex.Message}");
            }

            await Task.Delay(200);
        }
    }

    /// <summary>
    /// Merges pkgs.org and supplemental results into the output model.
    /// </summary>
    static IList<DistroPackageAvailability> MergeResults(
        Dictionary<string, Dictionary<string, List<DotnetDistroPackage>>> pkgsOrg,
        Dictionary<string, Dictionary<string, List<DotnetDistroPackage>>> supplemental,
        DistroSourceCollection distroSources)
    {
        // Combine all distro keys
        var allKeys = new HashSet<string>(pkgsOrg.Keys);
        foreach (var key in supplemental.Keys)
            allKeys.Add(key);

        // Group by distro name (everything before the last space+version, or the whole key)
        var byDistro = new Dictionary<string, List<(string key, string version)>>();
        foreach (var key in allKeys.OrderBy(k => k))
        {
            string distroName = key;
            string version = "";

            // Try to split "Ubuntu 24.04" → ("Ubuntu", "24.04")
            int lastSpace = key.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                string possibleVersion = key[(lastSpace + 1)..];
                if (possibleVersion.Length > 0 && (char.IsDigit(possibleVersion[0]) || possibleVersion == "Edge" || possibleVersion == "Rawhide" || possibleVersion == "Rolling" || possibleVersion == "Sisyphus" || possibleVersion == "Cooker"))
                {
                    distroName = key[..lastSpace];
                    version = possibleVersion;
                }
            }

            if (!byDistro.TryGetValue(distroName, out var versions))
            {
                versions = [];
                byDistro[distroName] = versions;
            }
            versions.Add((key, version));
        }

        var result = new List<DistroPackageAvailability>();
        foreach (var (distroName, versions) in byDistro.OrderBy(kv => kv.Key))
        {
            var releases = new List<DistroReleasePackages>();
            foreach (var (key, version) in versions)
            {
                var feeds = new Dictionary<string, IList<DotnetDistroPackage>>();

                if (pkgsOrg.TryGetValue(key, out var pkgFeeds))
                {
                    foreach (var (feedName, packages) in pkgFeeds)
                        feeds[feedName] = packages;
                }

                if (supplemental.TryGetValue(key, out var suppFeeds))
                {
                    foreach (var (feedName, packages) in suppFeeds)
                        feeds[feedName] = packages;
                }

                releases.Add(new DistroReleasePackages(
                    key,
                    version.Length > 0 ? version : "rolling",
                    feeds.Count > 0 ? feeds : null));
            }

            result.Add(new DistroPackageAvailability(distroName, releases));
        }

        return result;
    }
}
