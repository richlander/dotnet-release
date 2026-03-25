using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dotnet.Release.Cve;
using Dotnet.Release.CveHandler;

// dotnet-cve-enricher: Agentic CLI for synthesizing, validating, and enriching .NET CVE data.
//
// Designed for both human and AI agent usage:
//   --json   Emit structured JSON to stdout (for agent piping) instead of writing files
//   --month  Operate on a single month (fine-grained; agents compose these)
//   --all    Operate across the full tree (batch; humans use this)
//
// Exit codes: 0 = success, 1 = validation errors, 2 = runtime error

if (args.Length < 1)
{
    PrintUsage();
    return 1;
}

// Parse command and arguments
string command = args[0].ToLowerInvariant();
string? inputPath = null;
bool jsonOutput = false;
bool skipUrls = false;
bool quietMode = false;
string? month = null;
bool all = false;

for (int i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--json":
            jsonOutput = true;
            break;
        case "--skip-urls":
            skipUrls = true;
            break;
        case "--quiet" or "-q":
            quietMode = true;
            break;
        case "--all":
            all = true;
            break;
        case "--month" when i + 1 < args.Length:
            month = args[++i];
            break;
        default:
            if (!args[i].StartsWith('-'))
            {
                inputPath = args[i];
            }
            break;
    }
}

if (inputPath is null)
{
    PrintUsage();
    return 1;
}

try
{
    return command switch
    {
        "synthesize" => await RunSynthesize(inputPath, month, all, jsonOutput, skipUrls),
        "validate" => await RunValidate(inputPath, skipUrls, quietMode, jsonOutput),
        "update" => await RunUpdate(inputPath, skipUrls, jsonOutput),
        _ => PrintUsage()
    };
}
catch (Exception ex)
{
    if (jsonOutput)
    {
        WriteJsonError(ex.Message);
    }
    else
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
    return 2;
}

// ---- Synthesize command ----

async Task<int> RunSynthesize(string releaseNotesPath, string? targetMonth, bool processAll, bool json, bool skipUrlFetch)
{
    if (!Directory.Exists(releaseNotesPath))
    {
        return ReportError($"Directory not found: {releaseNotesPath}", json);
    }

    var timelinePath = Path.Combine(releaseNotesPath, "timeline");
    var cutoffDate = processAll ? null : FindEarliestTimelineCveDate(timelinePath);

    // Scan releases.json for CVE data grouped by month
    var releasesByCveMonth = await ScanReleasesForCves(releaseNotesPath, cutoffDate, targetMonth);

    if (releasesByCveMonth.Count == 0)
    {
        if (json)
        {
            WriteSynthesizeResult(new SynthesizeResult([], 0, 0, 0));
        }
        else
        {
            Log("No months with CVE data to synthesize");
        }
        return 0;
    }

    var results = new List<FileResult>();
    int createdCount = 0;
    int skippedCount = 0;
    int errorCount = 0;

    foreach (var (yearMonth, releases) in releasesByCveMonth.OrderBy(kvp => kvp.Key))
    {
        try
        {
            var parts = yearMonth.Split('-');
            var year = parts[0];
            var monthStr = parts[1];

            var monthDir = Path.Combine(timelinePath, year, monthStr);
            var cveJsonPath = Path.Combine(monthDir, "cve.json");

            if (File.Exists(cveJsonPath) && !processAll)
            {
                LogQuiet($"Skipping {yearMonth} (cve.json already exists)");
                skippedCount++;
                continue;
            }

            Log($"Generating cve.json for {yearMonth}...");

            // Fetch MSRC data
            Dictionary<string, MsrcCveData>? msrcData = null;
            if (!skipUrlFetch)
            {
                var msrcId = MsrcClient.GetMsrcId(int.Parse(year), int.Parse(monthStr));
                Log($"  Fetching MSRC data for {msrcId}...");
                msrcData = await MsrcClient.FetchDataAsync(msrcId);
            }

            var cveRecords = BuildCveRecords(yearMonth, releases, msrcData);

            if (json)
            {
                results.Add(new FileResult(cveJsonPath, cveRecords));
            }
            else
            {
                Directory.CreateDirectory(monthDir);
                string jsonStr = JsonSerializer.Serialize(cveRecords, CveSerializerContext.Default.CveRecords);
                await File.WriteAllTextAsync(cveJsonPath, jsonStr);
                Log($"  Created {cveJsonPath} ({cveRecords.Disclosures.Count} CVEs from {releases.Count} releases)");
            }
            createdCount++;
        }
        catch (Exception ex)
        {
            Log($"  Error processing {yearMonth}: {ex.Message}");
            errorCount++;
        }
    }

    if (json)
    {
        WriteSynthesizeResult(new SynthesizeResult(results, createdCount, skippedCount, errorCount));
    }
    else
    {
        Console.WriteLine($"Summary: Created {createdCount}, Skipped {skippedCount}, Errors {errorCount}");
    }

    return errorCount > 0 ? 1 : 0;
}

// ---- Validate command ----

async Task<int> RunValidate(string path, bool skipUrlChecks, bool quiet, bool json)
{
    var cveFiles = CveLoader.FindCveFiles(path);
    if (cveFiles.Count == 0)
    {
        return ReportError($"No cve.json files found at: {path}", json);
    }

    var fileReports = new List<ValidationFileReport>();
    int failureCount = 0;

    foreach (var cveFile in cveFiles)
    {
        var report = await ValidateCveFile(cveFile, skipUrlChecks);
        fileReports.Add(report);

        if (report.Errors.Count > 0)
        {
            failureCount++;
        }

        if (!json)
        {
            if (report.Errors.Count == 0 && report.Warnings.Count == 0)
            {
                if (!quiet)
                {
                    Console.WriteLine($"Validating: {cveFile}");
                    Console.WriteLine("  ✓ All validations passed");
                }
            }
            else
            {
                Console.WriteLine($"Validating: {cveFile}");
                foreach (var warning in report.Warnings)
                    Console.WriteLine($"  ⚠ {warning}");
                foreach (var error in report.Errors)
                    Console.WriteLine($"  ✗ {error}");
            }
            if (!quiet || report.Errors.Count > 0)
            {
                Console.WriteLine();
            }
        }
    }

    if (json)
    {
        WriteValidationResult(new ValidationResult(
            fileReports,
            fileReports.Count - failureCount,
            failureCount
        ));
    }
    else
    {
        Console.WriteLine($"Validation complete: {fileReports.Count - failureCount} succeeded, {failureCount} failed");
    }

    return failureCount > 0 ? 1 : 0;
}

// ---- Update command ----

async Task<int> RunUpdate(string path, bool skipUrlChecks, bool json)
{
    var cveFiles = CveLoader.FindCveFiles(path);
    if (cveFiles.Count == 0)
    {
        return ReportError($"No cve.json files found at: {path}", json);
    }

    var results = new List<FileResult>();
    int successCount = 0;
    int failureCount = 0;

    foreach (var cveFile in cveFiles)
    {
        try
        {
            Log($"Updating: {cveFile}");

            using var stream = File.OpenRead(cveFile);
            var cveRecords = await CveLoader.DeserializeAsync(stream);

            if (cveRecords is null)
            {
                Log($"  ERROR: Failed to deserialize JSON");
                failureCount++;
                continue;
            }

            var generated = CveDictionaryGenerator.GenerateAll(cveRecords);
            var cveCommits = CveDictionaryGenerator.GenerateCommits(cveRecords);

            IList<Cve> updatedCves = cveRecords.Disclosures;
            if (!skipUrlChecks)
            {
                updatedCves = await UpdateCvssScores(cveRecords.Disclosures);
                updatedCves = await UpdateCnaDataFromMsrc(cveFile, updatedCves);
            }

            var updated = cveRecords with
            {
                Disclosures = updatedCves,
                CveReleases = generated.CveReleases,
                ProductCves = generated.ProductCves,
                PackageCves = generated.PackageCves,
                ProductName = generated.ProductName,
                ReleaseCves = generated.ReleaseCves,
                SeverityCves = generated.SeverityCves,
                CveCommits = cveCommits
            };

            if (json)
            {
                results.Add(new FileResult(cveFile, updated));
            }
            else
            {
                string jsonStr = JsonSerializer.Serialize(updated, CveSerializerContext.Default.CveRecords);
                await File.WriteAllTextAsync(cveFile, jsonStr);
                var updateMsg = skipUrlChecks ? "dictionaries and cve_commits" : "dictionaries, cve_commits, and CVSS scores";
                Log($"  ✓ Updated {updateMsg}");
            }
            successCount++;
        }
        catch (Exception ex)
        {
            Log($"  ERROR: {ex.Message}");
            failureCount++;
        }
    }

    if (json)
    {
        WriteUpdateResult(new UpdateResult(results, successCount, failureCount));
    }
    else
    {
        Console.WriteLine($"Update complete: {successCount} succeeded, {failureCount} failed");
    }

    return failureCount > 0 ? 1 : 0;
}

// ---- Validation logic ----

async Task<ValidationFileReport> ValidateCveFile(string filePath, bool skipUrlChecks)
{
    var errors = new List<string>();
    var warnings = new List<string>();

    try
    {
        using var jsonStream = File.OpenRead(filePath);
        var cves = await CveLoader.DeserializeAsync(jsonStream);

        if (cves is null)
        {
            errors.Add("Failed to deserialize JSON");
            return new ValidationFileReport(filePath, errors, warnings);
        }

        ValidateTaxonomy(cves, errors);
        ValidateVersionCoherence(cves, errors);
        ValidateReleaseFields(cves, errors);
        ValidateReleaseVersionFormats(cves, errors);
        ValidateCommitKeyFormat(cves, errors);
        ValidateForeignKeys(cves, errors, warnings);
        ValidateDictionaries(cves, errors);

        if (!skipUrlChecks)
        {
            await ValidateUrls(cves, errors);
            await ValidateMsrcData(filePath, cves, errors);
        }
    }
    catch (JsonException ex)
    {
        errors.Add($"JSON parsing error: {ex.Message}");
    }
    catch (Exception ex)
    {
        errors.Add($"Unexpected error: {ex.Message}");
    }

    return new ValidationFileReport(filePath, errors, warnings);
}

static void ValidateTaxonomy(CveRecords cves, List<string> errors)
{
    var validProducts = new[] { "dotnet-runtime", "dotnet-aspnetcore", "dotnet-windows-desktop", "dotnet-sdk" };
    var validPlatforms = new[] { "linux", "macos", "windows", "all" };
    var validArchitectures = new[] { "arm", "arm64", "x64", "x86", "all" };
    var validSeverities = new[] { "critical", "high", "medium", "low" };
    var validCnas = new[] { "microsoft" };

    if (cves.Products is not null)
    {
        foreach (var product in cves.Products)
        {
            if (!validProducts.Contains(product.Name, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Invalid product name: '{product.Name}'");
            }
        }
    }

    if (cves.Disclosures is not null)
    {
        foreach (var cve in cves.Disclosures)
        {
            if (cve.Platforms is not null)
            {
                foreach (var platform in cve.Platforms)
                {
                    if (!validPlatforms.Contains(platform, StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add($"Invalid platform in {cve.Id}: '{platform}'");
                    }
                }
            }

            if (cve.Architectures is not null)
            {
                foreach (var arch in cve.Architectures)
                {
                    if (!validArchitectures.Contains(arch, StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add($"Invalid architecture in {cve.Id}: '{arch}'");
                    }
                }
            }

            if (!string.IsNullOrEmpty(cve.Cvss.Severity))
            {
                if (!validSeverities.Contains(cve.Cvss.Severity, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"Invalid severity in {cve.Id}: '{cve.Cvss.Severity}'");
                }
            }

            if (cve.Cna is not null)
            {
                if (!validCnas.Any(v => string.Equals(v, cve.Cna.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add($"Invalid CNA in {cve.Id}: '{cve.Cna.Name}'");
                }
            }
        }
    }
}

static void ValidateVersionCoherence(CveRecords cves, List<string> errors)
{
    if (cves.Products is not null)
    {
        foreach (var product in cves.Products)
        {
            if (!IsVersionCoherent(product.MinVulnerable, product.MaxVulnerable, product.Fixed))
            {
                errors.Add($"Incoherent versions for {product.CveId} in product {product.Name}: min={product.MinVulnerable}, max={product.MaxVulnerable}, fixed={product.Fixed}");
            }
        }
    }

    if (cves.Packages is not null)
    {
        foreach (var package in cves.Packages)
        {
            if (!IsVersionCoherent(package.MinVulnerable, package.MaxVulnerable, package.Fixed))
            {
                errors.Add($"Incoherent versions for {package.CveId} in package {package.Name}: min={package.MinVulnerable}, max={package.MaxVulnerable}, fixed={package.Fixed}");
            }
        }
    }
}

static void ValidateReleaseFields(CveRecords cves, List<string> errors)
{
    if (cves.Products is not null)
    {
        foreach (var product in cves.Products)
        {
            if (string.IsNullOrEmpty(product.Release))
            {
                errors.Add($"Product '{product.Name}' for {product.CveId} has null or empty release field");
            }
        }
    }
}

static void ValidateReleaseVersionFormats(CveRecords cves, List<string> errors)
{
    if (cves.Products is not null)
    {
        foreach (var product in cves.Products)
        {
            if (!string.IsNullOrEmpty(product.Release) && !IsTwoPartVersion(product.Release))
            {
                errors.Add($"Product '{product.Name}' for {product.CveId} has invalid release version '{product.Release}' (must be two-part like '9.0')");
            }
        }
    }
}

static void ValidateCommitKeyFormat(CveRecords cves, List<string> errors)
{
    if (cves.Commits is null) return;

    var commitKeyPattern = new Regex(@"^([a-zA-Z0-9-]+)@([a-f0-9]{7})$", RegexOptions.IgnoreCase);

    foreach (var kvp in cves.Commits)
    {
        var key = kvp.Key;
        var commit = kvp.Value;

        var match = commitKeyPattern.Match(key);
        if (!match.Success)
        {
            errors.Add($"Commit key '{key}' has invalid format (expected: repo@short_hash)");
            continue;
        }

        var keyRepo = match.Groups[1].Value;
        var keyShortHash = match.Groups[2].Value;

        if (!string.Equals(keyRepo, commit.Repo, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Commit key '{key}' repo mismatch: key='{keyRepo}', commit='{commit.Repo}'");
        }

        if (!commit.Hash.StartsWith(keyShortHash, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Commit key '{key}' hash mismatch: key='{keyShortHash}', hash='{commit.Hash}'");
        }
    }
}

static void ValidateForeignKeys(CveRecords cves, List<string> errors, List<string> warnings)
{
    var cveIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (cves.Disclosures is not null)
    {
        foreach (var cve in cves.Disclosures) cveIds.Add(cve.Id);
    }

    var commitHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (cves.Commits is not null)
    {
        foreach (var commit in cves.Commits.Keys) commitHashes.Add(commit);
    }

    if (cves.Products is not null)
    {
        foreach (var product in cves.Products)
        {
            if (!cveIds.Contains(product.CveId))
                errors.Add($"Product '{product.Name}' references unknown CVE: {product.CveId}");

            if (cves.Commits is not null)
            {
                if (product.Commits is null)
                    errors.Add($"Product '{product.Name}' for {product.CveId} has null commits");
                else if (product.Commits.Count == 0)
                    errors.Add($"Product '{product.Name}' for {product.CveId} has empty commits");
                else
                {
                    foreach (var commit in product.Commits)
                    {
                        if (string.IsNullOrWhiteSpace(commit))
                            errors.Add($"Product '{product.Name}' for {product.CveId} has empty commit hash");
                        else if (!commitHashes.Contains(commit))
                            warnings.Add($"Product '{product.Name}' for {product.CveId} references commit not in .commits: {commit}");
                    }
                }
            }
        }
    }

    if (cves.Packages is not null)
    {
        foreach (var package in cves.Packages)
        {
            if (!cveIds.Contains(package.CveId))
                errors.Add($"Package '{package.Name}' references unknown CVE: {package.CveId}");

            if (cves.Commits is not null)
            {
                if (package.Commits is null)
                    errors.Add($"Package '{package.Name}' for {package.CveId} has null commits");
                else if (package.Commits.Count == 0)
                    errors.Add($"Package '{package.Name}' for {package.CveId} has empty commits");
                else
                {
                    foreach (var commit in package.Commits)
                    {
                        if (string.IsNullOrWhiteSpace(commit))
                            errors.Add($"Package '{package.Name}' for {package.CveId} has empty commit hash");
                        else if (!commitHashes.Contains(commit))
                            warnings.Add($"Package '{package.Name}' for {package.CveId} references commit not in .commits: {commit}");
                    }
                }
            }
        }
    }

    // Validate cve_commits references
    if (cves.CveCommits is not null)
    {
        foreach (var kvp in cves.CveCommits)
        {
            if (!cveIds.Contains(kvp.Key))
                errors.Add($"cve_commits references unknown CVE: {kvp.Key}");
            foreach (var commitHash in kvp.Value)
            {
                if (!commitHashes.Contains(commitHash))
                    errors.Add($"CVE {kvp.Key} references unknown commit: {commitHash}");
            }
        }
    }

    // Check orphans
    var referencedCves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (cves.Products is not null)
        foreach (var p in cves.Products) referencedCves.Add(p.CveId);
    if (cves.Packages is not null)
        foreach (var p in cves.Packages) referencedCves.Add(p.CveId);

    foreach (var cveId in cveIds)
    {
        if (!referencedCves.Contains(cveId))
            errors.Add($"CVE {cveId} is not referenced by any product or package");
    }

    var referencedCommits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (cves.CveCommits is not null)
    {
        foreach (var kvp in cves.CveCommits)
            foreach (var c in kvp.Value) referencedCommits.Add(c);
    }

    foreach (var ch in commitHashes)
    {
        if (!referencedCommits.Contains(ch))
            errors.Add($"Commit {ch} is not referenced by any CVE");
    }
}

static void ValidateDictionaries(CveRecords cveRecords, List<string> errors)
{
    var expected = CveDictionaryGenerator.GenerateAll(cveRecords);

    ValidateDictionary(cveRecords.CveReleases, expected.CveReleases, "cve_releases", errors);
    ValidateDictionary(cveRecords.ProductCves, expected.ProductCves, "product_cves", errors);
    ValidateDictionary(cveRecords.PackageCves, expected.PackageCves, "package_cves", errors);
    ValidateDictionary(cveRecords.ProductName, expected.ProductName, "product_name", errors);
    ValidateDictionary(cveRecords.ReleaseCves, expected.ReleaseCves, "release_cves", errors);
    ValidateDictionary(cveRecords.SeverityCves, expected.SeverityCves, "severity_cves", errors);

    var expectedCveCommits = CveDictionaryGenerator.GenerateCommits(cveRecords);
    if (expectedCveCommits.Count > 0)
    {
        ValidateDictionary(cveRecords.CveCommits, expectedCveCommits, "cve_commits", errors);
    }
}

static void ValidateDictionary<T>(
    IDictionary<string, T>? actual,
    IDictionary<string, T>? expected,
    string dictionaryName,
    List<string> errors)
{
    if (expected is null && actual is null) return;

    if (expected is null || actual is null)
    {
        errors.Add($"{dictionaryName}: Dictionary is {(actual is null ? "missing" : "unexpected")}");
        return;
    }

    foreach (var key in expected.Keys)
    {
        if (!actual.ContainsKey(key))
            errors.Add($"{dictionaryName}: Missing key '{key}'");
    }

    foreach (var key in actual.Keys)
    {
        if (!expected.ContainsKey(key))
            errors.Add($"{dictionaryName}: Unexpected key '{key}'");
    }

    foreach (var key in expected.Keys.Intersect(actual.Keys))
    {
        var expectedValue = expected[key];
        var actualValue = actual[key];

        if (expectedValue is IList<string> expectedList && actualValue is IList<string> actualList)
        {
            var expectedSorted = expectedList.OrderBy(x => x).ToList();
            var actualSorted = actualList.OrderBy(x => x).ToList();

            if (!expectedSorted.SequenceEqual(actualSorted))
            {
                errors.Add($"{dictionaryName}['{key}']: Expected [{string.Join(", ", expectedSorted)}], got [{string.Join(", ", actualSorted)}]");
            }
        }
        else if (!Equals(expectedValue, actualValue))
        {
            errors.Add($"{dictionaryName}['{key}']: Expected {expectedValue}, got {actualValue}");
        }
    }
}

static async Task ValidateUrls(CveRecords cves, List<string> errors)
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    client.DefaultRequestHeaders.Add("User-Agent", "dotnet-cve-enricher/0.1");

    var urls = new HashSet<string>();
    if (cves.Disclosures is not null)
    {
        foreach (var cve in cves.Disclosures)
        {
            if (cve.References is not null)
            {
                foreach (var url in cve.References) urls.Add(url);
            }
        }
    }
    if (cves.Commits is not null)
    {
        foreach (var commit in cves.Commits.Values) urls.Add(commit.Url);
    }

    var tasks = urls.Select(url => ValidateSingleUrl(client, url));
    var results = await Task.WhenAll(tasks);
    foreach (var error in results.Where(e => e is not null))
    {
        errors.Add(error!);
    }
}

static async Task<string?> ValidateSingleUrl(HttpClient client, string url)
{
    try
    {
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        return response.IsSuccessStatusCode ? null : $"URL returned {(int)response.StatusCode}: {url}";
    }
    catch (HttpRequestException ex)
    {
        return $"URL request failed: {url} - {ex.Message}";
    }
    catch (TaskCanceledException)
    {
        return $"URL request timeout: {url}";
    }
}

static async Task ValidateMsrcData(string filePath, CveRecords cves, List<string> errors)
{
    var msrcId = MsrcClient.GetMsrcIdFromPath(filePath);
    if (msrcId is null) return;

    var msrcData = await MsrcClient.FetchDataAsync(msrcId);
    if (msrcData is null)
    {
        errors.Add("MSRC: Could not fetch MSRC data");
        return;
    }

    foreach (var cve in cves.Disclosures)
    {
        if (msrcData.TryGetValue(cve.Id, out var msrcCve))
        {
            if (cve.Cvss.Score != msrcCve.Score)
                errors.Add($"MSRC: {cve.Id} score mismatch - Expected: {msrcCve.Score}, Actual: {cve.Cvss.Score}");
            if (cve.Cvss.Vector != msrcCve.Vector)
                errors.Add($"MSRC: {cve.Id} vector mismatch");
            if (cve.Weakness != msrcCve.Weakness)
                errors.Add($"MSRC: {cve.Id} weakness/CWE mismatch");
            if (!string.IsNullOrEmpty(msrcCve.Impact))
            {
                var actualImpact = cve.Cna?.Impact;
                if (string.IsNullOrEmpty(actualImpact))
                    errors.Add($"MSRC: {cve.Id} missing cna.impact - Expected: '{msrcCve.Impact}'");
                else if (actualImpact != msrcCve.Impact)
                    errors.Add($"MSRC: {cve.Id} cna.impact mismatch");
            }
        }
    }
}

// ---- Update helpers ----

static async Task<IList<Cve>> UpdateCvssScores(IList<Cve> cves)
{
    var updatedCves = new List<Cve>();
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "dotnet-cve-enricher/0.1");

    foreach (var cve in cves)
    {
        try
        {
            var response = await client.GetStringAsync($"https://cveawg.mitre.org/api/cve/{cve.Id}");
            var jsonDoc = JsonDocument.Parse(response);

            decimal baseScore = 0.0m;
            string baseSeverity = "";
            string? weakness = null;

            if (jsonDoc.RootElement.TryGetProperty("containers", out var containers) &&
                containers.TryGetProperty("cna", out var cna))
            {
                if (cna.TryGetProperty("metrics", out var metrics) && metrics.GetArrayLength() > 0)
                {
                    var firstMetric = metrics[0];
                    if (firstMetric.TryGetProperty("cvssV3_1", out var cvssV3_1))
                    {
                        baseScore = cvssV3_1.TryGetProperty("baseScore", out var se) ? se.GetDecimal() : 0.0m;
                        baseSeverity = cvssV3_1.TryGetProperty("baseSeverity", out var sv) ? sv.GetString() ?? "" : "";
                    }
                }

                if (cna.TryGetProperty("problemTypes", out var problemTypes) && problemTypes.GetArrayLength() > 0)
                {
                    var firstProblem = problemTypes[0];
                    if (firstProblem.TryGetProperty("descriptions", out var descriptions) && descriptions.GetArrayLength() > 0)
                    {
                        if (descriptions[0].TryGetProperty("cweId", out var cweIdElement))
                        {
                            weakness = cweIdElement.GetString();
                        }
                    }
                }
            }

            var updatedCvss = cve.Cvss with { Score = baseScore, Severity = baseSeverity };
            updatedCves.Add(cve with { Cvss = updatedCvss, Weakness = weakness });
        }
        catch
        {
            updatedCves.Add(cve);
        }

        await Task.Delay(500); // Rate limiting
    }

    return updatedCves;
}

static async Task<IList<Cve>> UpdateCnaDataFromMsrc(string filePath, IList<Cve> cves)
{
    var msrcId = MsrcClient.GetMsrcIdFromPath(filePath);
    if (msrcId is null) return cves;

    var msrcData = await MsrcClient.FetchDataAsync(msrcId);
    if (msrcData is null) return cves;

    var updatedCves = new List<Cve>();
    foreach (var cve in cves)
    {
        if (msrcData.TryGetValue(cve.Id, out var msrcCve))
        {
            var updatedCna = cve.Cna ?? new Cna("microsoft");
            bool hasUpdates = false;

            if (!string.IsNullOrEmpty(msrcCve.CnaSeverity))
            {
                updatedCna = updatedCna with { Severity = msrcCve.CnaSeverity };
                hasUpdates = true;
            }
            if (!string.IsNullOrEmpty(msrcCve.Impact))
            {
                updatedCna = updatedCna with { Impact = msrcCve.Impact };
                hasUpdates = true;
            }
            if (msrcCve.Acknowledgments is { Count: > 0 })
            {
                updatedCna = updatedCna with { Acknowledgments = msrcCve.Acknowledgments };
                hasUpdates = true;
            }
            if (msrcCve.Faqs is { Count: > 0 })
            {
                updatedCna = updatedCna with { Faq = msrcCve.Faqs };
                hasUpdates = true;
            }

            updatedCves.Add(hasUpdates ? cve with { Cna = updatedCna } : cve);
        }
        else
        {
            updatedCves.Add(cve);
        }
    }

    return updatedCves;
}

// ---- Synthesize helpers ----

static async Task<Dictionary<string, List<ReleaseWithCves>>> ScanReleasesForCves(
    string releaseNotesPath, DateOnly? cutoffDate, string? targetMonth)
{
    var releasesByCveMonth = new Dictionary<string, List<ReleaseWithCves>>();

    foreach (var majorVersionDir in Directory.GetDirectories(releaseNotesPath))
    {
        var majorVersion = Path.GetFileName(majorVersionDir);
        if (!majorVersion.Contains('.') || majorVersion == "timeline") continue;

        var releasesJsonPath = Path.Combine(majorVersionDir, "releases.json");
        if (!File.Exists(releasesJsonPath)) continue;

        try
        {
            var releasesJson = await File.ReadAllTextAsync(releasesJsonPath);
            var releasesDoc = JsonDocument.Parse(releasesJson);

            if (!releasesDoc.RootElement.TryGetProperty("releases", out var releasesArray)) continue;

            foreach (var release in releasesArray.EnumerateArray())
            {
                if (!release.TryGetProperty("release-date", out var releaseDateProp)) continue;
                var releaseDateStr = releaseDateProp.GetString();
                if (string.IsNullOrEmpty(releaseDateStr) || !DateOnly.TryParse(releaseDateStr, out var releaseDate)) continue;

                if (cutoffDate != null && releaseDate >= cutoffDate.Value) continue;

                var yearMonth = $"{releaseDate.Year:D4}-{releaseDate.Month:D2}";
                if (targetMonth != null && yearMonth != targetMonth) continue;

                if (!release.TryGetProperty("cve-list", out var cveListArray)) continue;
                if (cveListArray.ValueKind == JsonValueKind.Null || cveListArray.GetArrayLength() == 0) continue;

                if (!release.TryGetProperty("release-version", out var versionProp)) continue;
                var version = versionProp.GetString();
                if (string.IsNullOrEmpty(version)) continue;

                var cveIds = new List<string>();
                foreach (var cveEntry in cveListArray.EnumerateArray())
                {
                    if (cveEntry.TryGetProperty("cve-id", out var cveIdProp))
                    {
                        var cveId = cveIdProp.GetString();
                        if (!string.IsNullOrEmpty(cveId)) cveIds.Add(cveId);
                    }
                }
                if (cveIds.Count == 0) continue;

                if (!releasesByCveMonth.ContainsKey(yearMonth))
                {
                    releasesByCveMonth[yearMonth] = [];
                }

                releasesByCveMonth[yearMonth].Add(new ReleaseWithCves(version, majorVersion, releaseDate, cveIds));
            }
        }
        catch
        {
            // Skip unparseable releases.json
        }
    }

    return releasesByCveMonth;
}

static DateOnly? FindEarliestTimelineCveDate(string timelinePath)
{
    if (!Directory.Exists(timelinePath)) return null;

    DateOnly? earliest = null;

    foreach (var yearDir in Directory.GetDirectories(timelinePath))
    {
        if (!int.TryParse(Path.GetFileName(yearDir), out var yearNum)) continue;

        foreach (var monthDir in Directory.GetDirectories(yearDir))
        {
            if (!int.TryParse(Path.GetFileName(monthDir), out var monthNum)) continue;

            if (File.Exists(Path.Combine(monthDir, "cve.json")))
            {
                var date = new DateOnly(yearNum, monthNum, 1);
                if (earliest is null || date < earliest.Value) earliest = date;
            }
        }
    }

    return earliest;
}

static CveRecords BuildCveRecords(string yearMonth, List<ReleaseWithCves> releases, Dictionary<string, MsrcCveData>? msrcData)
{
    var parts = yearMonth.Split('-');
    var year = int.Parse(parts[0]);
    var month = int.Parse(parts[1]);
    var monthName = new DateTime(year, month, 1).ToString("MMMM");

    var allCveIds = releases.SelectMany(r => r.CveIds).Distinct().OrderBy(id => id).ToList();

    var disclosures = new List<Cve>();
    foreach (var cveId in allCveIds)
    {
        MsrcCveData? msrcCve = null;
        msrcData?.TryGetValue(cveId, out msrcCve);

        var earliestRelease = releases
            .Where(r => r.CveIds.Contains(cveId))
            .OrderBy(r => r.ReleaseDate)
            .First();

        var cvss = new Cvss(
            Version: "3.1",
            Vector: msrcCve?.Vector ?? "",
            Score: msrcCve?.Score ?? 0.0m,
            Severity: "",
            Source: "microsoft"
        );

        var timeline = new Timeline(
            Disclosure: new Event(earliestRelease.ReleaseDate, "Publicly disclosed"),
            Fixed: new Event(earliestRelease.ReleaseDate, $"Fixed in {earliestRelease.Version}")
        );

        Cna? cna = null;
        if (msrcCve is not null &&
            (!string.IsNullOrEmpty(msrcCve.Impact) ||
             !string.IsNullOrEmpty(msrcCve.CnaSeverity) ||
             msrcCve.Acknowledgments is not null ||
             msrcCve.Faqs is not null))
        {
            cna = new Cna(
                Name: "microsoft",
                Severity: msrcCve.CnaSeverity,
                Impact: msrcCve.Impact,
                Acknowledgments: msrcCve.Acknowledgments,
                Faq: msrcCve.Faqs
            );
        }

        disclosures.Add(new Cve(
            Id: cveId,
            Problem: msrcCve?.Impact ?? "Security Vulnerability",
            Description: [$"A security vulnerability exists in .NET. See {cveId} for details."],
            Cvss: cvss,
            Timeline: timeline,
            Platforms: ["all"],
            Architectures: ["all"],
            References:
            [
                $"https://msrc.microsoft.com/update-guide/vulnerability/{cveId}",
                $"https://nvd.nist.gov/vuln/detail/{cveId}"
            ],
            Weakness: msrcCve?.Weakness,
            Cna: cna
        ));
    }

    var products = new List<Product>();
    foreach (var release in releases)
    {
        foreach (var cveId in release.CveIds)
        {
            products.Add(new Product(
                CveId: cveId,
                Name: "dotnet-runtime",
                MinVulnerable: release.MajorVersion,
                MaxVulnerable: release.Version,
                Fixed: release.Version,
                Release: release.MajorVersion,
                Commits: []
            ));
        }
    }

    var generated = CveDictionaryGenerator.GenerateAll(new CveRecords(
        LastUpdated: "",
        Title: "",
        Disclosures: disclosures,
        Products: products,
        Packages: []
    ));

    return new CveRecords(
        LastUpdated: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        Title: $".NET {monthName} {year}",
        Disclosures: disclosures,
        Products: products,
        Packages: [],
        Commits: null,
        ProductName: generated.ProductName,
        ProductCves: generated.ProductCves,
        PackageCves: generated.PackageCves,
        ReleaseCves: generated.ReleaseCves,
        SeverityCves: generated.SeverityCves,
        CveReleases: generated.CveReleases,
        CveCommits: null
    );
}

// ---- Shared helpers ----

static bool IsVersionCoherent(string minVersion, string maxVersion, string fixedVersion)
{
    try
    {
        var min = ParseSemVer(minVersion);
        var max = ParseSemVer(maxVersion);
        var fix = ParseSemVer(fixedVersion);
        if (!min.HasValue || !max.HasValue || !fix.HasValue) return true;
        return CompareSemVer(min.Value, max.Value) <= 0 && CompareSemVer(max.Value, fix.Value) < 0;
    }
    catch
    {
        return true;
    }
}

static (Version version, string? prerelease)? ParseSemVer(string versionString)
{
    try
    {
        int dashIndex = versionString.IndexOf('-');
        string versionPart = dashIndex > 0 ? versionString[..dashIndex] : versionString;
        string? prerelease = dashIndex > 0 ? versionString[(dashIndex + 1)..] : null;
        return Version.TryParse(versionPart, out var version) ? (version, prerelease) : null;
    }
    catch
    {
        return null;
    }
}

static int CompareSemVer((Version version, string? prerelease) a, (Version version, string? prerelease) b)
{
    int versionCompare = a.version.CompareTo(b.version);
    if (versionCompare != 0) return versionCompare;

    if (a.prerelease is null && b.prerelease is null) return 0;
    if (a.prerelease is null) return 1;
    if (b.prerelease is null) return -1;
    return string.Compare(a.prerelease, b.prerelease, StringComparison.OrdinalIgnoreCase);
}

static bool IsTwoPartVersion(string version)
{
    var parts = version.Split('.');
    return parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _);
}

void Log(string message) => Console.Error.WriteLine(message);
void LogQuiet(string message) => Console.Error.WriteLine(message);

static int ReportError(string message, bool json)
{
    if (json)
    {
        WriteJsonError(message);
    }
    else
    {
        Console.Error.WriteLine($"Error: {message}");
    }
    return 2;
}

static void WriteJsonError(string message) =>
    Console.WriteLine(JsonSerializer.Serialize(new ErrorResult(message), ToolJsonContext.Default.ErrorResult));

static void WriteSynthesizeResult(SynthesizeResult data) =>
    Console.WriteLine(JsonSerializer.Serialize(data, ToolJsonContext.Default.SynthesizeResult));

static void WriteValidationResult(ValidationResult data) =>
    Console.WriteLine(JsonSerializer.Serialize(data, ToolJsonContext.Default.ValidationResult));

static void WriteUpdateResult(UpdateResult data) =>
    Console.WriteLine(JsonSerializer.Serialize(data, ToolJsonContext.Default.UpdateResult));

static int PrintUsage()
{
    Console.Error.WriteLine("""
        dotnet-cve-enricher: Synthesize, validate, and enrich .NET CVE disclosure data.

        Usage:
          dotnet-cve-enricher synthesize <release-notes-path> [options]
          dotnet-cve-enricher validate <path> [options]
          dotnet-cve-enricher update <path> [options]

        Synthesize options:
          --month <YYYY-MM>  Synthesize one month only
          --all              Synthesize all months (even if cve.json exists)
          --skip-urls        Skip MSRC API calls (faster, offline)
          --json             Emit generated records as JSON to stdout

        Validate options:
          --skip-urls        Skip URL and MSRC validation
          --quiet, -q        Only show files with errors
          --json             Emit structured validation report as JSON

        Update options:
          --skip-urls        Skip CVE.org and MSRC API calls
          --json             Emit updated records as JSON (no file write)

        Exit codes:
          0  Success
          1  Validation errors found
          2  Runtime error

        Examples:
          dotnet-cve-enricher synthesize ~/git/core/release-notes
          dotnet-cve-enricher synthesize ~/git/core/release-notes --month 2024-03 --json
          dotnet-cve-enricher validate ~/git/core/release-notes/timeline --skip-urls -q
          dotnet-cve-enricher update ~/git/core/release-notes/timeline/2025/01/cve.json --json
        """);
    return 1;
}

// ---- JSON output types ----

record FileResult(string Path, CveRecords Content);
record SynthesizeResult(List<FileResult> Files, int Created, int Skipped, int Errors);
record ValidationFileReport(string Path, List<string> Errors, List<string> Warnings);
record ValidationResult(List<ValidationFileReport> Files, int Succeeded, int Failed);
record UpdateResult(List<FileResult> Files, int Succeeded, int Failed);
record ErrorResult(string Error);
record ReleaseWithCves(string Version, string MajorVersion, DateOnly ReleaseDate, List<string> CveIds);

// JSON serializer context for tool output (AOT-compatible)
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(SynthesizeResult))]
[JsonSerializable(typeof(ValidationResult))]
[JsonSerializable(typeof(UpdateResult))]
[JsonSerializable(typeof(ErrorResult))]
partial class ToolJsonContext : JsonSerializerContext
{
}
