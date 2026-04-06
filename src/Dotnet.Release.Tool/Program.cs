using System.Globalization;
using Dotnet.Release.Client;
using Dotnet.Release.Graph;

using var httpClient = new HttpClient();
var graph = new ReleaseNotesGraph(httpClient);

try
{
    if (args.Length == 0)
    {
        return await PrintOverviewAsync(graph);
    }

    var command = args[0].ToLowerInvariant();

    if (command is "help" or "--help" or "-h")
    {
        PrintUsage();
        return 0;
    }

    if (command is "skill")
    {
        return PrintSkill();
    }

    return command switch
    {
        "overview" => await PrintOverviewAsync(graph),
        "releases" => await PrintReleasesAsync(graph, args),
        "release" => await PrintReleaseAsync(graph, args),
        "downloads" => await PrintDownloadsAsync(graph, args),
        "timeline" => await PrintTimelineAsync(graph, args),
        "cves" => await PrintCvesAsync(graph, args),
        _ => UnknownCommand(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static int UnknownCommand(string commandName)
{
    Console.Error.WriteLine($"Unknown command: {commandName}");
    Console.Error.WriteLine();
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: dotnet-release [command] [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Broad navigation:");
    Console.Error.WriteLine("  dotnet-release                   Show a release-index overview");
    Console.Error.WriteLine("  dotnet-release overview          Show latest supported releases and security status");
    Console.Error.WriteLine("  dotnet-release releases [--all]  List major releases");
    Console.Error.WriteLine("  dotnet-release release <ver>     Show recent patches for a major release");
    Console.Error.WriteLine("  dotnet-release downloads <ver> [component|band] [--rid <rid>]");
    Console.Error.WriteLine("  dotnet-release timeline [period] Show the release timeline by year, month, or day");
    Console.Error.WriteLine("  dotnet-release skill             Print agent guidance for release graph questions");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Targeted query:");
    Console.Error.WriteLine("  dotnet-release cves [-n <months>] [--product <name>] [--package <name>]");
    Console.Error.WriteLine("  dotnet-release cves since <date> [--product <name>] [--package <name>]");
    Console.Error.WriteLine("    <date> accepts YYYY, YYYY-MM, or YYYY-MM-DD");
    Console.Error.WriteLine("    timeline periods accept YYYY, YYYY-MM, YYYY-MM-DD, or separate year/month/day args");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Notes:");
    Console.Error.WriteLine("  This tool is currently pinned to the dotnet/core release-index branch.");
}

static async Task<int> PrintOverviewAsync(ReleaseNotesGraph graph)
{
    var llms = await graph.GetLlmsIndexAsync()
        ?? throw new InvalidOperationException("Failed to load llms.json from the release-index branch.");

    Console.WriteLine(".NET release graph overview");
    Console.WriteLine($"Source: {ReleaseNotes.GitHubBaseUri}");
    Console.WriteLine($"Latest major: {llms.LatestMajor ?? "n/a"}");
    Console.WriteLine($"Latest LTS major: {llms.LatestLtsMajor ?? "n/a"}");
    Console.WriteLine($"Latest patch date: {FormatDate(llms.LatestPatchDate)}");
    Console.WriteLine($"Latest security patch date: {FormatDate(llms.LatestSecurityPatchDate)}");

    if (llms.SupportedMajorReleases is { Count: > 0 } supported &&
        llms.Embedded?.Patches is { Count: > 0 } patches)
    {
        Console.WriteLine();
        Console.WriteLine("Supported releases:");

        foreach (var version in supported)
        {
            if (!patches.TryGetValue(version, out var patch))
            {
                continue;
            }

            var security = patch.Security ? "security" : "regular";
            Console.WriteLine(
                $"  {version,-6} {DisplayReleaseType(patch.ReleaseType),-3} {DisplayPhase(patch.SupportPhase),-11} {patch.Version,-12} sdk {patch.SdkVersion,-8} {security}");
        }
    }

    return 0;
}

static async Task<int> PrintReleasesAsync(ReleaseNotesGraph graph, string[] args)
{
    var showAll = args.Skip(1).Any(arg => string.Equals(arg, "--all", StringComparison.OrdinalIgnoreCase));
    var summary = graph.GetReleasesSummary();
    var releases = showAll
        ? await summary.GetAllVersionsAsync()
        : await summary.GetSupportedVersionsAsync();

    Console.WriteLine(showAll ? "All .NET major releases" : "Supported .NET major releases");

    foreach (var release in releases)
    {
        var security = release.HasSecurityFixes ? "security" : "regular";
        var cveText = release.CveCount > 0 ? $" cves:{release.CveCount}" : string.Empty;

        Console.WriteLine(
            $"  {release.Version,-6} {DisplayReleaseType(release.ReleaseType),-3} {DisplayPhase(release.Phase),-11} sdk {(release.LatestSdkRelease ?? "-"),-8} {security}{cveText}");
    }

    return 0;
}

static async Task<int> PrintReleaseAsync(ReleaseNotesGraph graph, string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: dotnet-release release <major-version>");
        return 1;
    }

    var version = args[1];
    var releases = graph.GetReleasesSummary();
    var release = await releases.GetVersionAsync(version);
    var manifest = await graph.GetManifestAsync(version);

    if (release is null)
    {
        Console.Error.WriteLine($"Version not found: {version}");
        return 1;
    }

    var navigator = graph.GetReleaseNavigator(version);
    var majorIndex = await graph.GetPatchReleaseIndexAsync(version)
        ?? throw new InvalidOperationException($"Failed to load release index for {version}.");
    var patches = (await navigator.GetAllPatchesAsync())
        .OrderByDescending(p => p.ReleaseDate)
        .ThenByDescending(p => p.Version, StringComparer.Ordinal)
        .Take(8)
        .ToList();
    var latestSdk = release.LatestSdkRelease
        ?? majorIndex.Embedded?.Patches?
            .OrderByDescending(p => p.Date)
            .ThenByDescending(p => p.Version, StringComparer.Ordinal)
            .Select(p => p.SdkVersion)
            .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version));

    Console.WriteLine($".NET {version}");
    Console.WriteLine($"Type: {DisplayReleaseType(manifest?.ReleaseType ?? release.ReleaseType)}");
    Console.WriteLine($"Phase: {DisplayPhase(manifest?.SupportPhase ?? release.Phase)}");
    Console.WriteLine($"Supported: {(manifest?.Supported ?? release.IsSupported ? "yes" : "no")}");
    Console.WriteLine($"Target framework: {manifest?.TargetFramework ?? "n/a"}");
    Console.WriteLine($"GA date: {FormatDate(manifest?.GaDate ?? release.ReleaseDate)}");
    Console.WriteLine($"EOL date: {FormatDate(manifest?.EolDate ?? release.EolDate)}");
    Console.WriteLine($"Latest SDK: {latestSdk ?? "n/a"}");
    Console.WriteLine($"Downloads: {(majorIndex.Links.ContainsKey(LinkRelations.Downloads) ? "available" : "not exposed in graph")}");

    if (patches.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Recent patches:");

        foreach (var patch in patches)
        {
            Console.WriteLine(
                $"  {patch.Version,-12} {FormatDate(patch.ReleaseDate),-12} {(patch.IsSecurityUpdate ? "security" : "regular")}");
        }
    }

    return 0;
}

static async Task<int> PrintDownloadsAsync(ReleaseNotesGraph graph, string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: dotnet-release downloads <major-version> [component|band] [--rid <rid>]");
        return 1;
    }

    var version = args[1];
    string? selector = null;
    string? rid = null;

    for (var i = 2; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--rid":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("The --rid option expects a value.");
                    return 1;
                }

                rid = args[++i];
                break;

            default:
                if (selector is not null)
                {
                    Console.Error.WriteLine($"Unexpected downloads argument: {args[i]}");
                    return 1;
                }

                selector = args[i];
                break;
        }
    }

    var majorIndex = await graph.GetPatchReleaseIndexAsync(version);
    if (majorIndex is null)
    {
        Console.Error.WriteLine($"Version not found: {version}");
        return 1;
    }

    if (!majorIndex.Links.TryGetValue(LinkRelations.Downloads, out var downloadsLink))
    {
        Console.WriteLine($".NET {version} downloads");
        Console.WriteLine("Downloads available: no");
        Console.WriteLine("This major version does not currently expose the downloads subtree in the release graph.");
        return 0;
    }

    var downloadsIndex = await graph.FollowLinkAsync<DownloadsIndex>(downloadsLink)
        ?? throw new InvalidOperationException($"Failed to load downloads index for {version}.");

    if (string.IsNullOrWhiteSpace(selector))
    {
        return await PrintDownloadsIndexAsync(graph, downloadsIndex);
    }

    return await PrintDownloadDetailsAsync(graph, downloadsIndex, selector, rid);
}

static async Task<int> PrintDownloadsIndexAsync(ReleaseNotesGraph graph, DownloadsIndex downloadsIndex)
{
    Console.WriteLine($".NET {downloadsIndex.Version} downloads");
    Console.WriteLine("Downloads available: yes");

    if (downloadsIndex.Embedded?.Components is { Count: > 0 } components)
    {
        Console.WriteLine();
        Console.WriteLine("Components:");

        foreach (var component in components)
        {
            var countText = await GetComponentDownloadCountTextAsync(graph, component);
            Console.WriteLine($"  {component.Name,-14} {countText,-13} {component.Title}");
        }
    }

    if (downloadsIndex.Embedded?.FeatureBands is { Count: > 0 } bands)
    {
        Console.WriteLine();
        Console.WriteLine("SDK feature bands:");

        foreach (var band in bands)
        {
            var countText = await GetFeatureBandDownloadCountTextAsync(graph, band);
            Console.WriteLine($"  {band.Version,-10} {DisplayPhase(band.SupportPhase),-11} {countText,-13} {band.Title}");
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Try `dotnet-release downloads {downloadsIndex.Version} runtime --rid linux-x64` for a specific asset.");
    return 0;
}

static async Task<string> GetComponentDownloadCountTextAsync(ReleaseNotesGraph graph, ComponentEntry component)
{
    if (component.Links is null || !component.Links.TryGetValue(HalTerms.Self, out var selfLink))
    {
        return "n/a";
    }

    var details = await graph.FollowLinkAsync<ComponentDownload>(selfLink);
    return details?.Embedded?.Downloads.Count is int count ? $"{count} entries" : "n/a";
}

static async Task<string> GetFeatureBandDownloadCountTextAsync(ReleaseNotesGraph graph, FeatureBandEntry band)
{
    if (band.Links is null || !band.Links.TryGetValue(HalTerms.Self, out var selfLink))
    {
        return "n/a";
    }

    var details = await graph.FollowLinkAsync<SdkDownloadInfo>(selfLink);
    return details?.Embedded?.Downloads.Count is int count ? $"{count} entries" : "n/a";
}

static async Task<int> PrintDownloadDetailsAsync(
    ReleaseNotesGraph graph,
    DownloadsIndex downloadsIndex,
    string selector,
    string? rid)
{
    var normalizedSelector = selector.StartsWith("sdk-", StringComparison.OrdinalIgnoreCase)
        ? selector["sdk-".Length..]
        : selector;

    var component = downloadsIndex.Embedded?.Components?.FirstOrDefault(entry =>
        string.Equals(entry.Name, selector, StringComparison.OrdinalIgnoreCase));

    if (component is not null)
    {
        var selfLink = component.Links is not null && component.Links.TryGetValue(HalTerms.Self, out var link)
            ? link
            : null;

        if (selfLink is null)
        {
            Console.Error.WriteLine($"Downloads entry is missing a self link for component {component.Name}.");
            return 1;
        }

        var details = await graph.FollowLinkAsync<ComponentDownload>(selfLink)
            ?? throw new InvalidOperationException($"Failed to load downloads for {component.Name}.");

        return PrintComponentDownload(details, rid);
    }

    var band = downloadsIndex.Embedded?.FeatureBands?.FirstOrDefault(entry =>
        string.Equals(entry.Version, normalizedSelector, StringComparison.OrdinalIgnoreCase));

    if (band is not null)
    {
        var selfLink = band.Links is not null && band.Links.TryGetValue(HalTerms.Self, out var link)
            ? link
            : null;

        if (selfLink is null)
        {
            Console.Error.WriteLine($"Downloads entry is missing a self link for SDK feature band {band.Version}.");
            return 1;
        }

        var details = await graph.FollowLinkAsync<SdkDownloadInfo>(selfLink)
            ?? throw new InvalidOperationException($"Failed to load downloads for SDK feature band {band.Version}.");

        return PrintSdkDownload(details, rid);
    }

    Console.Error.WriteLine($"Unknown downloads target: {selector}");
    Console.Error.WriteLine($"Use `dotnet-release downloads {downloadsIndex.Version}` to list available components and feature bands.");
    return 1;
}

static int PrintComponentDownload(ComponentDownload details, string? rid)
{
    Console.WriteLine($".NET {details.Version} {details.Component} downloads");
    Console.WriteLine("Downloads available: yes");
    if (!string.IsNullOrWhiteSpace(details.Description))
    {
        Console.WriteLine(details.Description);
    }

    var entries = (details.Embedded?.Downloads ?? [])
        .Select(pair => new DownloadEntryView(
            pair.Key,
            pair.Value.Name,
            pair.Value.Rid,
            pair.Value.Os,
            pair.Value.Arch,
            pair.Value.HashAlgorithm,
            GetHref(pair.Value.Links, "download"),
            GetHref(pair.Value.Links, "hash")))
        .ToList();

    return PrintDownloadEntries(entries, rid);
}

static int PrintSdkDownload(SdkDownloadInfo details, string? rid)
{
    Console.WriteLine($".NET SDK {details.Version} downloads");
    Console.WriteLine("Downloads available: yes");
    Console.WriteLine($"Phase: {DisplayPhase(details.SupportPhase)}");
    if (!string.IsNullOrWhiteSpace(details.Description))
    {
        Console.WriteLine(details.Description);
    }

    var entries = (details.Embedded?.Downloads ?? [])
        .Select(pair => new DownloadEntryView(
            pair.Key,
            pair.Value.Name,
            pair.Value.Rid,
            pair.Value.Os,
            pair.Value.Arch,
            pair.Value.HashAlgorithm,
            GetHref(pair.Value.Links, "download"),
            GetHref(pair.Value.Links, "hash")))
        .ToList();

    return PrintDownloadEntries(entries, rid);
}

static int PrintDownloadEntries(IReadOnlyList<DownloadEntryView> entries, string? rid)
{
    var filtered = string.IsNullOrWhiteSpace(rid)
        ? entries.OrderBy(entry => entry.Rid, StringComparer.Ordinal).ToList()
        : entries
            .Where(entry => string.Equals(entry.Rid, rid, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Rid, StringComparer.Ordinal)
            .ToList();

    if (filtered.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine(string.IsNullOrWhiteSpace(rid)
            ? "No download entries found."
            : $"No download entry found for RID {rid}.");
        return string.IsNullOrWhiteSpace(rid) ? 0 : 1;
    }

    Console.WriteLine();
    Console.WriteLine($"Entries: {filtered.Count}");

    foreach (var entry in filtered)
    {
        Console.WriteLine();
        Console.WriteLine($"  {entry.Rid,-18} {entry.Name}");
        Console.WriteLine($"    platform: {entry.Os}/{entry.Arch}");
        Console.WriteLine($"    download: {entry.DownloadUrl ?? "n/a"}");
        Console.WriteLine($"    hash ({entry.HashAlgorithm}): {entry.HashUrl ?? "n/a"}");
    }

    return 0;
}

static string? GetHref(IReadOnlyDictionary<string, HalLink>? links, string relation)
    => links is not null && links.TryGetValue(relation, out var link) ? link.Href : null;

static async Task<int> PrintTimelineAsync(ReleaseNotesGraph graph, string[] args)
{
    var archives = graph.GetArchivesSummary();

    if (args.Length == 1)
    {
        var years = await archives.GetAllYearsAsync();
        Console.WriteLine(".NET release timeline");

        foreach (var year in years.OrderByDescending(y => y.Year, StringComparer.Ordinal))
        {
            var majors = year.MajorReleases is { Count: > 0 }
                ? string.Join(", ", year.MajorReleases)
                : "n/a";
            Console.WriteLine($"  {year.Year}   majors: {majors}");
        }

        return 0;
    }

    if (!TryParseTimelineTarget(args.Skip(1).ToArray(), out var target, out var error))
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine("Usage: dotnet-release timeline [YYYY | YYYY-MM | YYYY-MM-DD]");
        Console.Error.WriteLine("       dotnet-release timeline <year> [month] [day]");
        return 1;
    }

    var yearSummary = await archives.GetYearAsync(target.Year);

    if (yearSummary is null)
    {
        Console.Error.WriteLine($"Timeline year not found: {target.Year}");
        return 1;
    }

    if (target.Month is null)
    {
        var navigator = graph.GetArchiveNavigator(target.Year);
        var months = (await navigator.GetAllMonthsAsync())
            .OrderByDescending(m => m.Month, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($".NET release timeline for {target.Year}");

        foreach (var month in months)
        {
            var label = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(int.Parse(month.Month, CultureInfo.InvariantCulture));
            Console.WriteLine($"  {month.Month}  {label,-9} {(month.Security ? "security" : "regular")}");
        }

        return 0;
    }

    var monthIndex = await graph.GetMonthIndexAsync(target.Year, target.Month);
    if (monthIndex is null)
    {
        Console.Error.WriteLine($"Timeline period not found: {target.DisplayValue}");
        return 1;
    }

    return PrintTimelineMonth(monthIndex, target.Day);
}

static int PrintTimelineMonth(HistoryMonthIndex monthIndex, DateOnly? day)
{
    var heading = day is null
        ? $".NET release timeline for {FormatMonthYear(monthIndex.Year, monthIndex.Month)}"
        : $".NET release timeline for {day:yyyy-MM-dd}";

    Console.WriteLine(heading);
    Console.WriteLine($"Month: {monthIndex.Year}-{monthIndex.Month}");
    Console.WriteLine($"Security month: {(monthIndex.Security ? "yes" : "no")}");

    if (monthIndex.Date is not null)
    {
        Console.WriteLine($"Primary release date: {FormatDate(monthIndex.Date)}");
    }

    var patches = (monthIndex.Embedded?.Patches ?? new Dictionary<string, PatchReleaseVersionIndexEntry>())
        .Select(kvp => new
        {
            Major = kvp.Value.MajorRelease ?? kvp.Key,
            Patch = kvp.Value
        })
        .Where(entry => day is null || DateOnly.FromDateTime(entry.Patch.Date.Date) == day.Value)
        .OrderByDescending(entry => entry.Patch.Date)
        .ThenByDescending(entry => entry.Patch.Version, StringComparer.Ordinal)
        .ToList();

    var disclosures = (monthIndex.Embedded?.Disclosures ?? Array.Empty<CveRecordSummary>())
        .Where(disclosure => day is null || disclosure.DisclosureDate == day.Value)
        .OrderByDescending(disclosure => disclosure.DisclosureDate)
        .ThenBy(disclosure => disclosure.Id, StringComparer.Ordinal)
        .ToList();

    if (patches.Count == 0 && disclosures.Count == 0 && day is not null)
    {
        Console.WriteLine();
        Console.WriteLine("No releases or security disclosures were published on that day.");
        return 0;
    }

    if (patches.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine(day is null ? "Patch releases:" : "Patch releases on this day:");

        foreach (var entry in patches)
        {
            var patch = entry.Patch;
            Console.WriteLine(
                $"  {entry.Major,-6} {patch.Version,-12} {FormatDate(patch.Date),-12} sdk {(patch.SdkVersion ?? "-"),-8} {DisplayPhase(patch.SupportPhase),-11} {(patch.Security ? "security" : "regular")}");
        }
    }

    if (disclosures.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine(day is null ? "Security disclosures:" : "Security disclosures on this day:");

        foreach (var disclosure in disclosures)
        {
            var cvss = disclosure.CvssScore?.ToString("0.0", CultureInfo.InvariantCulture) ?? "n/a";
            Console.WriteLine(
                $"  {disclosure.Id}  {FormatDateOnly(disclosure.DisclosureDate),-12} CVSS {cvss} {(disclosure.CvssSeverity ?? "n/a"),-8} {disclosure.Title}");
            PrintList("    releases", disclosure.AffectedReleases);
            PrintList("    products", disclosure.AffectedProducts);
            PrintList("    packages", disclosure.AffectedPackages);
        }
    }

    return 0;
}

static async Task<int> PrintCvesAsync(ReleaseNotesGraph graph, string[] args)
{
    if (!TryParseCveQuery(args, out var options, out var error))
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine("Usage: dotnet-release cves [-n <months>] [--product <name>] [--package <name>]");
        Console.Error.WriteLine("       dotnet-release cves since <date> [--product <name>] [--package <name>]");
        Console.Error.WriteLine("       <date> accepts YYYY, YYYY-MM, or YYYY-MM-DD");
        return 1;
    }

    var startDate = options.Since ?? DateTime.UtcNow.AddMonths(-(options.MonthsBack ?? 3));
    var archives = graph.GetArchivesSummary();
    var records = (await archives.GetCveRecordsInDateRangeAsync(
            startDate.Year,
            startDate.Month,
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month))
        .OrderByDescending(GetRecordSortDate)
        .ToList();

    var filterSuffix = BuildFilterDescription(options);
    var heading = options.Since is not null
        ? $".NET CVEs since {startDate:yyyy-MM-dd}{filterSuffix}"
        : $".NET CVEs in the last {options.MonthsBack ?? 3} month(s){filterSuffix}";

    var anyMatches = false;
    Console.WriteLine(heading);

    foreach (var record in records)
    {
        var matches = record.Disclosures
            .Where(disclosure => DisclosureMatches(record, disclosure, startDate, options))
            .OrderByDescending(disclosure => disclosure.Timeline.Disclosure.Date)
            .ToList();

        if (matches.Count == 0)
        {
            continue;
        }

        anyMatches = true;

        var maxScore = matches
            .Select(disclosure => disclosure.Cvss.Score)
            .DefaultIfEmpty(0m)
            .Max();

        Console.WriteLine();
        Console.WriteLine($"{record.Title} ({matches.Count} CVEs, max CVSS {maxScore:0.0})");

        foreach (var disclosure in matches)
        {
            Console.WriteLine(
                $"  {disclosure.Id}  {disclosure.Timeline.Disclosure.Date:yyyy-MM-dd}  CVSS {disclosure.Cvss.Score:0.0} {disclosure.Cvss.Severity}  {disclosure.Problem}");

            var matchedProducts = GetProductMatches(record, disclosure.Id, options.Product);
            if (matchedProducts.Count > 0)
            {
                Console.WriteLine($"    products: {string.Join("; ", matchedProducts)}");
            }

            var matchedPackages = GetPackageMatches(record, disclosure.Id, options.Package);
            if (matchedPackages.Count > 0)
            {
                Console.WriteLine($"    packages: {string.Join("; ", matchedPackages)}");
            }
        }
    }

    if (!anyMatches)
    {
        Console.WriteLine();
        Console.WriteLine("No matching CVEs found.");
    }

    return 0;
}

static string BuildFilterDescription(CveQueryOptions options)
{
    var filters = new List<string>();

    if (!string.IsNullOrWhiteSpace(options.Product))
    {
        filters.Add($"product contains \"{options.Product}\"");
    }

    if (!string.IsNullOrWhiteSpace(options.Package))
    {
        filters.Add($"package contains \"{options.Package}\"");
    }

    return filters.Count == 0 ? string.Empty : $" ({string.Join(", ", filters)})";
}

static bool DisclosureMatches(
    Dotnet.Release.Cve.CveRecords record,
    Dotnet.Release.Cve.Cve disclosure,
    DateTime startDate,
    CveQueryOptions options)
{
    var disclosureDate = disclosure.Timeline.Disclosure.Date.ToDateTime(TimeOnly.MinValue);
    if (disclosureDate < startDate.Date)
    {
        return false;
    }

    if (!string.IsNullOrWhiteSpace(options.Product) &&
        GetProductMatches(record, disclosure.Id, options.Product).Count == 0)
    {
        return false;
    }

    if (!string.IsNullOrWhiteSpace(options.Package) &&
        GetPackageMatches(record, disclosure.Id, options.Package).Count == 0)
    {
        return false;
    }

    return true;
}

static List<string> GetProductMatches(Dotnet.Release.Cve.CveRecords record, string cveId, string? filter)
    => record.Products
        .Where(product => product.CveId == cveId && NameMatches(product.Name, filter))
        .Select(product => $"{product.Name} {product.Release} -> fixed {product.Fixed}")
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

static List<string> GetPackageMatches(Dotnet.Release.Cve.CveRecords record, string cveId, string? filter)
    => record.Packages
        .Where(package => package.CveId == cveId && NameMatches(package.Name, filter))
        .Select(package => $"{package.Name} {package.Release} -> fixed {package.Fixed}")
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

static bool NameMatches(string name, string? filter)
    => string.IsNullOrWhiteSpace(filter) || name.Contains(filter, StringComparison.OrdinalIgnoreCase);

static DateTime GetRecordSortDate(Dotnet.Release.Cve.CveRecords records)
{
    var disclosureDate = records.Disclosures
        .Select(disclosure => disclosure.Timeline.Disclosure.Date.ToDateTime(TimeOnly.MinValue))
        .DefaultIfEmpty(DateTime.MinValue)
        .Max();

    return disclosureDate;
}

static bool TryParseCveQuery(string[] args, out CveQueryOptions options, out string? error)
{
    DateTime? since = null;
    int? monthsBack = null;
    string? product = null;
    string? package = null;

    for (var i = 1; i < args.Length; i++)
    {
        var arg = args[i];

        switch (arg)
        {
            case "recent":
            case "last":
                break;

            case "since" when i + 1 < args.Length:
                if (!TryParseSinceDate(args[++i], out var parsedDate))
                {
                    error = $"Could not parse date: {args[i]}";
                    options = default;
                    return false;
                }

                since = parsedDate;
                break;

            case "since":
                error = "The since filter expects a date value.";
                options = default;
                return false;

            case "-n":
            case "--months":
                if (i + 1 >= args.Length || !int.TryParse(args[++i], out var parsedMonths) || parsedMonths < 1)
                {
                    error = "The -n/--months option expects a positive integer.";
                    options = default;
                    return false;
                }

                monthsBack = parsedMonths;
                break;

            case "--product":
            case "product":
                if (i + 1 >= args.Length)
                {
                    error = "The product filter expects a value.";
                    options = default;
                    return false;
                }

                product = args[++i];
                break;

            case "--package":
            case "package":
                if (i + 1 >= args.Length)
                {
                    error = "The package filter expects a value.";
                    options = default;
                    return false;
                }

                package = args[++i];
                break;

            default:
                if (int.TryParse(arg, out var positionalMonths) && positionalMonths > 0)
                {
                    monthsBack = positionalMonths;
                    break;
                }

                error = $"Unrecognized cves argument: {arg}";
                options = default;
                return false;
        }
    }

    if (since is not null && monthsBack is not null)
    {
        error = "Use either `since <date>` or `-n <months>`, not both.";
        options = default;
        return false;
    }

    options = new CveQueryOptions(since, monthsBack, product, package);
    error = null;
    return true;
}

static bool TryParseSinceDate(string value, out DateTime parsed)
{
    var formats = new[]
    {
        "yyyy",
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "yyyy-MM",
        "yyyy/MM",
        "MMM yyyy",
        "MMMM yyyy"
    };

    if (DateTime.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out parsed))
    {
        parsed = parsed.Date;
        return true;
    }

    if (DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out parsed))
    {
        parsed = parsed.Date;
        return true;
    }

    return false;
}

static bool TryParseTimelineTarget(string[] values, out TimelineTarget target, out string? error)
{
    var parts = values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .ToArray();

    if (parts.Length == 1)
    {
        var token = parts[0];

        if (TryParseYearValue(token, out var year))
        {
            target = new TimelineTarget(year, null, null);
            error = null;
            return true;
        }

        if (TryParseYearMonthValue(token, out year, out var month))
        {
            target = new TimelineTarget(year, month, null);
            error = null;
            return true;
        }

        if (TryParseDateValue(token, out var day))
        {
            target = new TimelineTarget(
                day.Year.ToString("0000", CultureInfo.InvariantCulture),
                day.Month.ToString("00", CultureInfo.InvariantCulture),
                day);
            error = null;
            return true;
        }
    }
    else if (parts.Length == 2 &&
             TryParseYearValue(parts[0], out var year) &&
             TryParseMonthValue(parts[1], out var month))
    {
        target = new TimelineTarget(year, month, null);
        error = null;
        return true;
    }
    else if (parts.Length == 3 &&
             TryParseYearValue(parts[0], out var yearValue) &&
             TryParseMonthValue(parts[1], out var monthValue) &&
             TryParseDayValue(parts[2], out var dayValue) &&
             DateOnly.TryParseExact(
                 $"{yearValue}-{monthValue}-{dayValue}",
                 "yyyy-MM-dd",
                 CultureInfo.InvariantCulture,
                 DateTimeStyles.None,
                 out var date))
    {
        target = new TimelineTarget(yearValue, monthValue, date);
        error = null;
        return true;
    }

    error = "Could not parse the timeline period.";
    target = default;
    return false;
}

static bool TryParseYearValue(string value, out string year)
{
    if (value.Length == 4 &&
        int.TryParse(value, CultureInfo.InvariantCulture, out var parsedYear) &&
        parsedYear is >= 2000 and <= 3000)
    {
        year = parsedYear.ToString("0000", CultureInfo.InvariantCulture);
        return true;
    }

    year = string.Empty;
    return false;
}

static bool TryParseYearMonthValue(string value, out string year, out string month)
{
    var separators = new[] { '-', '/' };
    var parts = value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (parts.Length == 2 &&
        TryParseYearValue(parts[0], out year) &&
        TryParseMonthValue(parts[1], out month))
    {
        return true;
    }

    year = string.Empty;
    month = string.Empty;
    return false;
}

static bool TryParseDateValue(string value, out DateOnly date)
    => DateOnly.TryParseExact(
        value,
        ["yyyy-M-d", "yyyy-MM-dd", "yyyy/M/d", "yyyy/MM/dd"],
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out date);

static bool TryParseMonthValue(string value, out string month)
{
    if (int.TryParse(value, CultureInfo.InvariantCulture, out var parsedMonth) &&
        parsedMonth is >= 1 and <= 12)
    {
        month = parsedMonth.ToString("00", CultureInfo.InvariantCulture);
        return true;
    }

    month = string.Empty;
    return false;
}

static bool TryParseDayValue(string value, out string day)
{
    if (int.TryParse(value, CultureInfo.InvariantCulture, out var parsedDay) &&
        parsedDay is >= 1 and <= 31)
    {
        day = parsedDay.ToString("00", CultureInfo.InvariantCulture);
        return true;
    }

    day = string.Empty;
    return false;
}

static int PrintSkill()
{
    using var stream = typeof(Program).Assembly.GetManifestResourceStream("Dotnet.Release.Tool.SKILL.md");
    if (stream is null)
    {
        Console.Error.WriteLine("Error: SKILL.md resource not found.");
        return 1;
    }

    using var reader = new StreamReader(stream);
    Console.Write(reader.ReadToEnd());
    return 0;
}

static string DisplayReleaseType(Dotnet.Release.ReleaseType? releaseType) => releaseType switch
{
    Dotnet.Release.ReleaseType.LTS => "LTS",
    Dotnet.Release.ReleaseType.STS => "STS",
    _ => "n/a"
};

static string DisplayPhase(Dotnet.Release.SupportPhase? phase) => phase switch
{
    Dotnet.Release.SupportPhase.Active => "active",
    Dotnet.Release.SupportPhase.Preview => "preview",
    Dotnet.Release.SupportPhase.Maintenance => "maintenance",
    Dotnet.Release.SupportPhase.GoLive => "go-live",
    Dotnet.Release.SupportPhase.Eol => "eol",
    _ => "unknown"
};

static string FormatDate(DateTimeOffset? date)
    => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "n/a";

static string FormatDateOnly(DateOnly? date)
    => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "n/a";

static string FormatMonthYear(string year, string month)
{
    var name = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(int.Parse(month, CultureInfo.InvariantCulture));
    return $"{name} {year}";
}

static void PrintList(string label, IEnumerable<string>? values, int maxItems = 6)
{
    if (values is null)
    {
        return;
    }

    var items = values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (items.Count == 0)
    {
        return;
    }

    var shown = items.Take(maxItems).ToList();
    var suffix = items.Count > shown.Count ? $", ... (+{items.Count - shown.Count} more)" : string.Empty;
    Console.WriteLine($"{label}: {string.Join(", ", shown)}{suffix}");
}

internal readonly record struct CveQueryOptions(
    DateTime? Since,
    int? MonthsBack,
    string? Product,
    string? Package);

internal readonly record struct TimelineTarget(
    string Year,
    string? Month,
    DateOnly? Day)
{
    public string DisplayValue => Day?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        ?? (Month is null ? Year : $"{Year}-{Month}");
}

internal readonly record struct DownloadEntryView(
    string Key,
    string Name,
    string Rid,
    string Os,
    string Arch,
    string HashAlgorithm,
    string? DownloadUrl,
    string? HashUrl);
