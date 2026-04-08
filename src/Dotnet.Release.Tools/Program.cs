using System.Text.Json;
using Dotnet.Release;
using Dotnet.Release.Changes;
using Dotnet.Release.ChangesHandler;
using Dotnet.Release.IndexGenerator;
using Dotnet.Release.Releases;
using Dotnet.Release.Summary;
using Dotnet.Release.Support;
using Dotnet.Release.Tools;

// Usage: release-notes generate <type> <version> [path-or-url] [--template <file>]
//        release-notes generate <type> --export-template
//        release-notes generate changes <repo-path> --base <ref> --head <ref> [--branch <branch>] [--version <ver>] [--date <date>] [--output <file>]
//        release-notes generate version-index|timeline-index|llms-index|indexes <input-dir> [output-dir] [--url-root <url>]
//        release-notes verify <type> <version> [path-or-url]
//        release-notes query distro-packages --dotnet-version <ver> [--output <file>]
// Types: supported-os, os-packages, changes

if (args.Length >= 1 && args[0] == "skill")
{
    return PrintSkill();
}

if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

string command = args[0];

if (command == "query")
{
    return await HandleQueryAsync(args);
}

if (command == "verify")
{
    return await HandleVerifyAsync(args);
}

if (command != "generate")
{
    PrintUsage();
    return 1;
}

string type = args[1];

// Handle --export-template
if (args.Length > 2 && args[2] == "--export-template")
{
    switch (type)
    {
        case "supported-os":
            SupportedOsGenerator.ExportTemplate(Console.Out);
            break;
        case "os-packages":
            OsPackagesGenerator.ExportTemplate(Console.Out);
            break;
        case "dotnet-dependencies":
            DotnetDependenciesGenerator.ExportTemplate(Console.Out);
            break;
        case "dotnet-packages":
            DotnetPackagesGenerator.ExportTemplate(Console.Out);
            break;
        case "releases":
            ReleasesGenerator.ExportTemplate(Console.Out);
            break;
        default:
            Console.Error.WriteLine($"Unknown type: {type}");
            PrintUsage();
            return 1;
    }
    return 0;
}

// Changes generator: release-notes generate changes <repo-path> --base <ref> --head <ref> ...
if (type == "changes")
{
    return await HandleGenerateChangesAsync(args);
}

// Build metadata generator: release-notes generate build-metadata <repo-path> --base <ref> --head <ref> [--output file]
if (type == "build-metadata")
{
    return await HandleGenerateBuildMetadataAsync(args);
}

// Types that don't require a version number
if (type is "releases-index" or "releases" or "version-index" or "timeline-index" or "llms-index" or "indexes")
{
    string genPath = ".";
    string? genOutputPath = null;
    string? genTemplatePath = null;
    string? genUrlRoot = null;

    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--template" && i + 1 < args.Length)
        {
            genTemplatePath = args[++i];
        }
        else if (args[i] == "--url-root" && i + 1 < args.Length)
        {
            genUrlRoot = args[++i];
        }
        else if (!args[i].StartsWith('-'))
        {
            if (type is "version-index" or "timeline-index" or "llms-index" or "indexes")
            {
                // Index generators: first positional is input-dir, second is output-dir
                if (genPath == ".")
                {
                    genPath = args[i];
                }
                else
                {
                    genOutputPath = args[i];
                }
            }
            else
            {
                genPath = args[i];
            }
        }
    }

    return type switch
    {
        "releases-index" => await GenerateReleasesIndexAsync(genPath),
        "releases" => await GenerateReleasesAsync(genPath, genTemplatePath),
        "version-index" => await GenerateVersionIndexAsync(genPath, genOutputPath, genUrlRoot),
        "timeline-index" => await GenerateTimelineIndexAsync(genPath, genOutputPath, genUrlRoot),
        "llms-index" => await GenerateLlmsIndexAsync(genPath, genOutputPath, genUrlRoot),
        "indexes" => await GenerateAllIndexesAsync(genPath, genOutputPath, genUrlRoot),
        _ => 1
    };
}

if (args.Length < 3 || !decimal.TryParse(args[2], out _))
{
    PrintUsage();
    return 1;
}

string version = args[2];
string basePath = Location.GitHubBaseUri;
string? templatePath = null;

// Parse remaining args
for (int i = 3; i < args.Length; i++)
{
    if (args[i] == "--template" && i + 1 < args.Length)
    {
        templatePath = args[++i];
    }
    else if (!args[i].StartsWith('-'))
    {
        basePath = args[i];
    }
}

using var client = new HttpClient();
var path = AdaptivePath.Create(basePath, client);

switch (type)
{
    case "supported-os":
        return await GenerateSupportedOsAsync(path, version, client, templatePath);
    case "os-packages":
        return await GenerateOsPackagesAsync(path, version, client, templatePath);
    case "dotnet-dependencies":
        return await GenerateDotnetDependenciesAsync(path, version, client, templatePath);
    case "dotnet-packages":
        return await GenerateDotnetPackagesAsync(path, version, client, templatePath);
    default:
        Console.Error.WriteLine($"Unknown type: {type}");
        PrintUsage();
        return 1;
}

async Task<int> GenerateSupportedOsAsync(IAdaptivePath path, string version, HttpClient client, string? templatePath)
{
    string jsonPath = path.Combine(version, FileNames.SupportedOs);
    Console.Error.WriteLine($"Reading {jsonPath}...");

    using var stream = await path.GetStreamAsync(jsonPath);
    var matrix = await JsonSerializer.DeserializeAsync(stream, SupportedOSMatrixSerializerContext.Default.SupportedOSMatrix)
        ?? throw new InvalidOperationException("Failed to deserialize supported-os.json");

    // Fetch release metadata for support phase and release type
    string? supportPhase = null;
    string? releaseType = null;

    string releasesPath = path.Combine(version, FileNames.Releases);
    Console.Error.WriteLine($"Reading {releasesPath}...");

    try
    {
        using var releasesStream = await path.GetStreamAsync(releasesPath);
        var overview = await JsonSerializer.DeserializeAsync(releasesStream, MajorReleaseOverviewSerializerContext.Default.MajorReleaseOverview);

        if (overview is not null)
        {
            supportPhase = overview.SupportPhase.ToDisplayName();
            releaseType = overview.ReleaseType.ToDisplayName();
            Console.Error.WriteLine($"Release status: {supportPhase}, {releaseType}");
        }
    }
    catch (Exception ex) when (ex is HttpRequestException or FileNotFoundException)
    {
        Console.Error.WriteLine($"Could not read releases.json: {ex.Message}");
    }

    TextWriter output;
    string? outputPath = null;

    if (path.SupportsLocalPaths)
    {
        outputPath = path.Combine(version, "supported-os.md");
        output = new StreamWriter(File.Open(outputPath, FileMode.Create));
    }
    else
    {
        output = Console.Out;
    }

    await SupportedOsGenerator.GenerateAsync(matrix, output, version, client, supportPhase: supportPhase, releaseType: releaseType, templatePath: templatePath);

    if (outputPath is not null)
    {
        await output.DisposeAsync();
        var info = new FileInfo(outputPath);
        Console.Error.WriteLine($"Generated {info.Length} bytes");
        Console.Error.WriteLine(info.FullName);
    }

    return 0;
}

async Task<int> GenerateOsPackagesAsync(IAdaptivePath path, string version, HttpClient client, string? templatePath)
{
    string jsonPath = path.Combine(version, "os-packages.json");
    Console.Error.WriteLine($"Reading {jsonPath}...");

    using var stream = await path.GetStreamAsync(jsonPath);
    var overview = await JsonSerializer.DeserializeAsync(stream, OSPackagesSerializerContext.Default.OSPackagesOverview)
        ?? throw new InvalidOperationException("Failed to deserialize os-packages.json");

    TextWriter output;
    string? outputPath = null;

    if (path.SupportsLocalPaths)
    {
        outputPath = path.Combine(version, "os-packages.md");
        output = new StreamWriter(File.Open(outputPath, FileMode.Create));
    }
    else
    {
        output = Console.Out;
    }

    OsPackagesGenerator.Generate(overview, output, version, templatePath: templatePath);

    if (outputPath is not null)
    {
        await output.DisposeAsync();
        var info = new FileInfo(outputPath);
        Console.Error.WriteLine($"Generated {info.Length} bytes");
        Console.Error.WriteLine(info.FullName);
    }

    return 0;
}

async Task<int> GenerateDotnetDependenciesAsync(IAdaptivePath path, string version, HttpClient client, string? templatePath)
{
    // Read distros/index.json
    string indexPath = path.Combine(version, FileNames.Directories.Distros, FileNames.Index);
    Console.Error.WriteLine($"Reading {indexPath}...");

    using var indexStream = await path.GetStreamAsync(indexPath);
    var index = await JsonSerializer.DeserializeAsync(indexStream, DistrosIndexSerializerContext.Default.DistrosIndex)
        ?? throw new InvalidOperationException("Failed to deserialize index.json");

    // Read distros/dependencies.json
    string depsPath = path.Combine(version, FileNames.Directories.Distros, FileNames.Dependencies);
    Console.Error.WriteLine($"Reading {depsPath}...");

    using var depsStream = await path.GetStreamAsync(depsPath);
    var dependencies = await JsonSerializer.DeserializeAsync(depsStream, DependenciesFileSerializerContext.Default.DependenciesFile)
        ?? throw new InvalidOperationException("Failed to deserialize dependencies.json");

    // Read each per-distro file
    var distros = new List<DistroPackageFile>();
    foreach (var distroFile in index.Distros.Keys)
    {
        string distroPath = path.Combine(version, FileNames.Directories.Distros, distroFile);
        Console.Error.WriteLine($"Reading {distroPath}...");

        using var distroStream = await path.GetStreamAsync(distroPath);
        var distro = await JsonSerializer.DeserializeAsync(distroStream, DistroPackageFileSerializerContext.Default.DistroPackageFile)
            ?? throw new InvalidOperationException($"Failed to deserialize {distroFile}");

        distros.Add(distro);
    }

    TextWriter output;
    string? outputPath = null;

    if (path.SupportsLocalPaths)
    {
        outputPath = path.Combine(version, "dotnet-dependencies.md");
        output = new StreamWriter(File.Open(outputPath, FileMode.Create));
    }
    else
    {
        output = Console.Out;
    }

    DotnetDependenciesGenerator.Generate(dependencies, index, distros, output, version, templatePath: templatePath);

    if (outputPath is not null)
    {
        await output.DisposeAsync();
        var info = new FileInfo(outputPath);
        Console.Error.WriteLine($"Generated {info.Length} bytes");
        Console.Error.WriteLine(info.FullName);
    }

    return 0;
}

async Task<int> GenerateDotnetPackagesAsync(IAdaptivePath path, string version, HttpClient client, string? templatePath)
{
    // Read distros/index.json
    string indexPath = path.Combine(version, FileNames.Directories.Distros, FileNames.Index);
    Console.Error.WriteLine($"Reading {indexPath}...");

    using var indexStream = await path.GetStreamAsync(indexPath);
    var index = await JsonSerializer.DeserializeAsync(indexStream, DistrosIndexSerializerContext.Default.DistrosIndex)
        ?? throw new InvalidOperationException("Failed to deserialize index.json");

    // Read each per-distro file
    var distros = new List<DistroPackageFile>();
    foreach (var distroFile in index.Distros.Keys)
    {
        string distroPath = path.Combine(version, FileNames.Directories.Distros, distroFile);
        Console.Error.WriteLine($"Reading {distroPath}...");

        using var distroStream = await path.GetStreamAsync(distroPath);
        var distro = await JsonSerializer.DeserializeAsync(distroStream, DistroPackageFileSerializerContext.Default.DistroPackageFile)
            ?? throw new InvalidOperationException($"Failed to deserialize {distroFile}");

        distros.Add(distro);
    }

    TextWriter output;
    string? outputPath = null;

    if (path.SupportsLocalPaths)
    {
        outputPath = path.Combine(version, "dotnet-packages.md");
        output = new StreamWriter(File.Open(outputPath, FileMode.Create));
    }
    else
    {
        output = Console.Out;
    }

    DotnetPackagesGenerator.Generate(index, distros, output, version, templatePath: templatePath);

    if (outputPath is not null)
    {
        await output.DisposeAsync();
        var info = new FileInfo(outputPath);
        Console.Error.WriteLine($"Generated {info.Length} bytes");
        Console.Error.WriteLine(info.FullName);
    }

    return 0;
}

async Task<int> GenerateReleasesIndexAsync(string basePath)
{
    Console.Error.WriteLine($"Generating {FileNames.ReleasesIndex} from {Path.GetFullPath(basePath)}...");

    string outputPath = Path.Combine(basePath, FileNames.ReleasesIndex);
    using var fileStream = File.Create(outputPath);

    await ReleasesIndexGenerator.GenerateAsync(basePath, fileStream, Console.Error);

    // Add trailing newline
    fileStream.WriteByte((byte)'\n');

    var info = new FileInfo(outputPath);
    Console.Error.WriteLine($"Generated {info.Length} bytes");
    Console.Error.WriteLine(info.FullName);
    return 0;
}

async Task<int> GenerateReleasesAsync(string basePath, string? templatePath)
{
    Console.Error.WriteLine($"Generating releases.md from {Path.GetFullPath(basePath)}...");

    string outputPath = Path.Combine(basePath, "releases.md");
    await using (var output = new StreamWriter(File.Open(outputPath, FileMode.Create)))
    {
        await ReleasesGenerator.GenerateAsync(basePath, output, Console.Error, templatePath);
    }

    var info = new FileInfo(outputPath);
    Console.Error.WriteLine($"Generated {info.Length} bytes");
    Console.Error.WriteLine(info.FullName);
    return 0;
}

(string InputDir, string OutputDir) PrepareIndexDirs(string inputDir, string? outputDir)
{
    var resolvedOutput = outputDir ?? inputDir;

    if (!Directory.Exists(inputDir))
    {
        throw new DirectoryNotFoundException($"Input directory not found: {inputDir}");
    }

    if (inputDir != resolvedOutput && !Directory.Exists(resolvedOutput))
    {
        Directory.CreateDirectory(resolvedOutput);
        Console.Error.WriteLine($"Created output directory: {resolvedOutput}");
    }

    return (inputDir, resolvedOutput);
}

async Task<List<MajorReleaseSummary>> LoadSummariesAsync(string inputDir, bool supportedOnly = false)
{
    return await ReleaseSummaryLoader.GetReleaseSummariesAsync(inputDir, supportedOnly)
        ?? throw new InvalidOperationException("Failed to generate release summaries.");
}

async Task<int> GenerateVersionIndexAsync(string inputDir, string? outputDir, string? urlRoot)
{
    var (input, output) = PrepareIndexDirs(inputDir, outputDir);

    if (urlRoot != null) Location.SetUrlRoot(urlRoot);

    Console.Error.WriteLine($"Generating version indexes from {Path.GetFullPath(input)}...");

    var summaries = await LoadSummariesAsync(input);
    await ReleaseIndexFiles.GenerateAsync(summaries, input, output);
    await DownloadsIndexFiles.GenerateAsync(summaries, output);

    Console.Error.WriteLine("Version index generation complete.");
    return 0;
}

async Task<int> GenerateTimelineIndexAsync(string inputDir, string? outputDir, string? urlRoot)
{
    var (input, output) = PrepareIndexDirs(inputDir, outputDir);

    if (urlRoot != null) Location.SetUrlRoot(urlRoot);

    Console.Error.WriteLine($"Generating timeline indexes from {Path.GetFullPath(input)}...");

    var summaries = await LoadSummariesAsync(input);
    ReleaseHistory history = ReleaseSummaryLoader.GetReleaseCalendar(summaries);
    ReleaseSummaryLoader.PopulateCveInformation(history, input);
    await ShipIndexFiles.GenerateAsync(input, output, history, summaries);

    Console.Error.WriteLine("Timeline index generation complete.");
    return 0;
}

async Task<int> GenerateLlmsIndexAsync(string inputDir, string? outputDir, string? urlRoot)
{
    var (input, output) = PrepareIndexDirs(inputDir, outputDir);

    if (urlRoot != null) Location.SetUrlRoot(urlRoot);

    Console.Error.WriteLine($"Generating llms.json from {Path.GetFullPath(input)}...");

    var summaries = await LoadSummariesAsync(input, supportedOnly: true);
    ReleaseHistory history = ReleaseSummaryLoader.GetReleaseCalendar(summaries);
    ReleaseSummaryLoader.PopulateCveInformation(history, input);
    await LlmsIndexFiles.GenerateAsync(input, output, summaries, history);

    Console.Error.WriteLine("LLMs index generation complete.");
    return 0;
}

async Task<int> GenerateAllIndexesAsync(string inputDir, string? outputDir, string? urlRoot)
{
    var (input, output) = PrepareIndexDirs(inputDir, outputDir);

    if (urlRoot != null) Location.SetUrlRoot(urlRoot);

    Console.Error.WriteLine($"Generating all indexes from {Path.GetFullPath(input)}...");

    // Load all summaries (version-index and timeline need all; llms needs supported only)
    var allSummaries = await LoadSummariesAsync(input);
    ReleaseHistory history = ReleaseSummaryLoader.GetReleaseCalendar(allSummaries);
    ReleaseSummaryLoader.PopulateCveInformation(history, input);

    // Version indexes
    await ReleaseIndexFiles.GenerateAsync(allSummaries, input, output);
    await DownloadsIndexFiles.GenerateAsync(allSummaries, output);

    // Timeline indexes
    await ShipIndexFiles.GenerateAsync(input, output, history, allSummaries);

    // LLMs index (filter to supported only)
    var supportedSummaries = allSummaries.Where(s => s.Lifecycle.Supported).ToList();
    await LlmsIndexFiles.GenerateAsync(input, output, supportedSummaries, history);

    Console.Error.WriteLine("All index generation complete.");
    return 0;
}

async Task<int> VerifyReleasesAsync(string basePath, string? version, bool skipHash)
{
    string scope = version is not null ? $".NET {version}" : "all supported versions";
    Console.Error.WriteLine($"Verifying release links for {scope} in {Path.GetFullPath(basePath)}...");

    using var client = new HttpClient();
    client.DefaultRequestHeaders.UserAgent.ParseAdd("release-notes-verifier/1.0");
    client.Timeout = TimeSpan.FromMinutes(5);

    var report = await ReleasesVerifier.VerifyAsync(basePath, client, Console.Error, skipHash, version);

    if (!report.HasIssues)
    {
        Console.Error.WriteLine("No issues found.");
        return 0;
    }

    var ctx = new ReleasesVerificationReportContext();
    ctx.Serialize(report, Console.Out);
    return 2;
}

async Task<int> HandleGenerateChangesAsync(string[] args)
{
    string? repoPath = null;
    string? baseRef = null;
    string? headRef = null;
    string? branch = null;
    string? version = "";
    string? date = "";
    string? outputPath = null;
    string? cveRepoPath = null;
    bool fetchLabels = false;
    bool jsonl = false;

    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--base" && i + 1 < args.Length)
        {
            baseRef = args[++i];
        }
        else if (args[i] == "--head" && i + 1 < args.Length)
        {
            headRef = args[++i];
        }
        else if (args[i] == "--branch" && i + 1 < args.Length)
        {
            branch = args[++i];
        }
        else if (args[i] == "--version" && i + 1 < args.Length)
        {
            version = args[++i];
        }
        else if (args[i] == "--date" && i + 1 < args.Length)
        {
            date = args[++i];
        }
        else if (args[i] == "--output" && i + 1 < args.Length)
        {
            outputPath = args[++i];
        }
        else if (args[i] == "--cve-repo" && i + 1 < args.Length)
        {
            cveRepoPath = args[++i];
        }
        else if (args[i] == "--labels")
        {
            fetchLabels = true;
        }
        else if (args[i] == "--jsonl")
        {
            jsonl = true;
        }
        else if (!args[i].StartsWith('-'))
        {
            repoPath = args[i];
        }
    }

    if (repoPath is null || baseRef is null || headRef is null)
    {
        Console.Error.WriteLine("Error: generate changes requires <repo-path>, --base <ref>, and --head <ref>");
        PrintUsage();
        return 1;
    }

    var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    if (string.IsNullOrEmpty(token))
    {
        Console.Error.WriteLine("Error: GITHUB_TOKEN environment variable is required for generate changes");
        return 1;
    }

    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("Authorization", $"token {token}");
    httpClient.DefaultRequestHeaders.Add("User-Agent", "release-notes-tool");

    var generator = new ChangesGenerator(httpClient);
    var records = await generator.GenerateAsync(
        repoPath,
        baseRef,
        headRef,
        branch ?? "",
        version ?? "",
        date ?? "",
        fetchLabels: fetchLabels
    );

    Console.Error.WriteLine($"Generated {records.Changes.Count} change entries with {records.Commits.Count} commits.");

    // Cross-reference with CVE data if --cve-repo is provided
    if (cveRepoPath is not null)
    {
        Console.Error.WriteLine("Cross-referencing with CVE data...");
        records = await CveCrossReference.ApplyAsync(records, cveRepoPath, repoPath, baseRef, headRef);
    }

    // Collapse to VMR-only commits: commit = dotnet_commit, drop source-repo entries
    records = ChangesGenerator.CollapseToVmrCommits(records);
    Console.Error.WriteLine($"Collapsed to {records.Commits.Count} VMR commit(s).");

    // Write output
    var writeAction = jsonl
        ? (Action<ChangeRecords, TextWriter>)ChangesGenerator.WriteJsonl
        : ChangesGenerator.Write;

    if (outputPath is not null)
    {
        using var writer = new StreamWriter(File.Open(outputPath, FileMode.Create));
        writeAction(records, writer);
        var info = new FileInfo(outputPath);
        Console.Error.WriteLine($"Wrote {info.Length} bytes to {info.FullName}");
    }
    else
    {
        writeAction(records, Console.Out);
        Console.Out.WriteLine();
    }

    return 0;
}

static int PrintSkill()
{
    using var stream = typeof(Program).Assembly.GetManifestResourceStream("Dotnet.Release.Tools.SKILL.md");

    if (stream is null)
    {
        Console.Error.WriteLine("Error: SKILL.md resource not found.");
        return 1;
    }

    using var reader = new StreamReader(stream);
    Console.WriteLine(reader.ReadToEnd());
    return 0;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: release-notes generate <type> <version> [path-or-url] [--template <file>]");
    Console.Error.WriteLine("       release-notes generate releases-index [path]");
    Console.Error.WriteLine("       release-notes generate releases [path] [--template <file>]");
    Console.Error.WriteLine("       release-notes generate changes <repo-path> --base <ref> --head <ref> [options]");
    Console.Error.WriteLine("       release-notes generate build-metadata <repo-path> --base <ref> --head <ref> [--output <file>]");
    Console.Error.WriteLine("       release-notes generate version-index <input-dir> [output-dir] [--url-root <url>]");
    Console.Error.WriteLine("       release-notes generate timeline-index <input-dir> [output-dir] [--url-root <url>]");
    Console.Error.WriteLine("       release-notes generate llms-index <input-dir> [output-dir] [--url-root <url>]");
    Console.Error.WriteLine("       release-notes generate indexes <input-dir> [output-dir] [--url-root <url>]");
    Console.Error.WriteLine("       release-notes generate <type> --export-template");
    Console.Error.WriteLine("       release-notes verify <type> <version> [path-or-url]");
    Console.Error.WriteLine("       release-notes verify releases [version] [path] [--skip-hash]");
    Console.Error.WriteLine("       release-notes query changes-previews [repo-path]");
    Console.Error.WriteLine("       release-notes query distro-packages --dotnet-version <ver> [--output <file>]");
    Console.Error.WriteLine("       release-notes skill");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Types: supported-os, os-packages, dotnet-dependencies, changes, build-metadata,");
    Console.Error.WriteLine("       releases-index, releases, version-index, timeline-index, llms-index, indexes");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Changes options:");
    Console.Error.WriteLine("  --base <ref>       Base git ref (tag, branch, or commit)");
    Console.Error.WriteLine("  --head <ref>       Head git ref (tag, branch, or commit)");
    Console.Error.WriteLine("  --branch <name>    Branch name for commit metadata (e.g. main, release/9.0)");
    Console.Error.WriteLine("  --version <ver>    Release version string for the output");
    Console.Error.WriteLine("  --date <date>      Release date (ISO 8601) for the output");
    Console.Error.WriteLine("  --cve-repo <path>  Path to dotnet/core clone (cross-references CVE data from release-index branch)");
    Console.Error.WriteLine("  --labels           Fetch PR labels from GitHub (adds labels field to each change)");
    Console.Error.WriteLine("  --jsonl            Output JSONL (one repo per line) instead of single JSON");
    Console.Error.WriteLine("  --output <file>    Write output to file instead of stdout");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Examples:");
    Console.Error.WriteLine("  release-notes generate supported-os 10.0");
    Console.Error.WriteLine("  release-notes generate os-packages 10.0");
    Console.Error.WriteLine("  release-notes generate dotnet-dependencies 11.0");
    Console.Error.WriteLine("  release-notes generate dotnet-dependencies 11.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  release-notes generate supported-os 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  release-notes generate os-packages --export-template > my-template.md");
    Console.Error.WriteLine("  release-notes generate releases-index ~/git/core/release-notes");
    Console.Error.WriteLine("  release-notes generate releases ~/git/core/release-notes");
    Console.Error.WriteLine("  release-notes generate releases --export-template > my-template.md");
    Console.Error.WriteLine("  release-notes generate changes ~/git/dotnet --base v11.0.0-preview.1.25060.1 --head v11.0.0-preview.2.26159.112");
    Console.Error.WriteLine("  release-notes generate changes ~/git/dotnet --base v11.0.0-preview.2.26159.112 --head main --branch main --output changes.json");
    Console.Error.WriteLine("  release-notes generate version-index ~/git/core/release-notes");
    Console.Error.WriteLine("  release-notes generate timeline-index ~/git/core/release-notes /tmp/output");
    Console.Error.WriteLine("  release-notes generate llms-index ~/git/core/release-notes");
    Console.Error.WriteLine("  release-notes generate indexes ~/git/core/release-notes --url-root https://raw.githubusercontent.com/dotnet/core/abc123");
    Console.Error.WriteLine("  release-notes verify supported-os 10.0");
    Console.Error.WriteLine("  release-notes verify supported-os 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  release-notes verify os-packages 10.0");
    Console.Error.WriteLine("  release-notes verify os-packages 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  release-notes verify releases ~/git/core/release-notes");
    Console.Error.WriteLine("  release-notes verify releases 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  release-notes verify releases 10.0.5 ~/git/core/release-notes");
    Console.Error.WriteLine("  release-notes verify releases ~/git/core/release-notes --skip-hash");
    Console.Error.WriteLine("  release-notes query changes-previews ~/git/dotnet");
    Console.Error.WriteLine("  release-notes query distro-packages --dotnet-version 9.0");
    Console.Error.WriteLine("  release-notes query distro-packages --dotnet-version 9.0 --output distro-packages.json");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Environment variables:");
    Console.Error.WriteLine("  GITHUB_TOKEN    GitHub API token (required for generate changes)");
    Console.Error.WriteLine("  PKGS_ORG_TOKEN  API token for pkgs.org (Gold+ subscription required)");
}

async Task<int> HandleVerifyAsync(string[] args)
{
    if (args.Length < 3)
    {
        PrintUsage();
        return 1;
    }

    string verifyType = args[1];

    if (verifyType is not "supported-os" and not "os-packages" and not "releases")
    {
        Console.Error.WriteLine($"Unknown verify type: {verifyType}");
        PrintUsage();
        return 1;
    }

    // "verify releases" accepts an optional version filter
    if (verifyType == "releases")
    {
        string verifyPath = ".";
        string? verifyVersion = null;
        bool skipHash = false;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--skip-hash")
            {
                skipHash = true;
            }
            else if (!args[i].StartsWith('-'))
            {
                // Distinguish version from path: versions contain only digits and dots
                if (verifyVersion is null && args[i].Length > 0 && char.IsDigit(args[i][0]) && args[i].All(c => char.IsDigit(c) || c == '.' || c == '-' || char.IsLetter(c)))
                {
                    // Could be a version like "10.0" or "10.0.5" or a path like "/tmp"
                    // Treat as version if it matches a version-like pattern (starts with digit.digit)
                    if (args[i].Contains('.') && char.IsDigit(args[i][0]) && !Path.IsPathRooted(args[i]) && !args[i].Contains(Path.DirectorySeparatorChar))
                    {
                        verifyVersion = args[i];
                    }
                    else
                    {
                        verifyPath = args[i];
                    }
                }
                else
                {
                    verifyPath = args[i];
                }
            }
        }

        return await VerifyReleasesAsync(verifyPath, verifyVersion, skipHash);
    }

    if (!decimal.TryParse(args[2], out _))
    {
        Console.Error.WriteLine($"Invalid version: {args[2]}");
        PrintUsage();
        return 1;
    }

    string version = args[2];
    string basePath = args.Length > 3 && !args[3].StartsWith('-') ? args[3] : Location.GitHubBaseUri;

    using var client = new HttpClient();
    var path = AdaptivePath.Create(basePath, client);

    if (verifyType == "supported-os")
    {
        string jsonPath = path.Combine(version, "supported-os.json");
        Console.Error.WriteLine($"Reading {jsonPath}...");

        using var stream = await path.GetStreamAsync(jsonPath);
        var matrix = await System.Text.Json.JsonSerializer.DeserializeAsync(stream, SupportedOSMatrixSerializerContext.Default.SupportedOSMatrix)
            ?? throw new InvalidOperationException("Failed to deserialize supported-os.json");

        Console.Error.WriteLine($"Verifying .NET {version} supported OS matrix...");
        var report = await SupportedOsVerifier.VerifyAsync(matrix, client, Console.Error);

        if (!report.HasIssues)
        {
            Console.Error.WriteLine("No issues found.");
            return 0;
        }

        var ctx = new SupportedOsReportContext();
        ctx.Serialize(report, Console.Out);
        return 2;
    }
    else
    {
        string jsonPath = path.Combine(version, "os-packages.json");
        Console.Error.WriteLine($"Reading {jsonPath}...");

        using var stream = await path.GetStreamAsync(jsonPath);
        var overview = await System.Text.Json.JsonSerializer.DeserializeAsync(stream, OSPackagesSerializerContext.Default.OSPackagesOverview)
            ?? throw new InvalidOperationException("Failed to deserialize os-packages.json");

        Console.Error.WriteLine($"Verifying .NET {version} OS packages...");
        var report = await OsPackagesVerifier.VerifyAsync(overview, client, Console.Error);

        if (!report.HasIssues)
        {
            Console.Error.WriteLine("No issues found.");
            return 0;
        }

        var ctx = new OsPackagesReportContext();
        ctx.Serialize(report, Console.Out);
        return 2;
    }
}

async Task<int> HandleQueryAsync(string[] args)
{
    if (args.Length < 2)
    {
        PrintUsage();
        return 1;
    }

    string queryType = args[1];

    if (queryType == "changes-previews")
    {
        string repoPath = args.Length > 2 && !args[2].StartsWith('-') ? args[2] : ".";
        var previews = await ChangesPreviewQuery.FindAsync(repoPath);

        if (previews.Count == 0)
        {
            Console.Error.WriteLine($"No preview refs found in {Path.GetFullPath(repoPath)}.");
            return 0;
        }

        foreach (var preview in previews)
        {
            Console.Out.WriteLine($"{preview.ReleaseVersion}\thead={preview.HeadRef}");
        }

        return 0;
    }

    if (queryType != "distro-packages")
    {
        Console.Error.WriteLine($"Unknown query type: {queryType}");
        PrintUsage();
        return 1;
    }

    string? dotnetVersion = null;
    string? outputPath = null;

    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--dotnet-version" && i + 1 < args.Length)
            dotnetVersion = args[++i];
        else if (args[i] == "--output" && i + 1 < args.Length)
            outputPath = args[++i];
    }

    if (dotnetVersion is null)
    {
        Console.Error.WriteLine("Error: --dotnet-version is required");
        PrintUsage();
        return 1;
    }

    string? token = Environment.GetEnvironmentVariable("PKGS_ORG_TOKEN");
    if (string.IsNullOrEmpty(token))
    {
        Console.Error.WriteLine("Error: PKGS_ORG_TOKEN environment variable is required.");
        Console.Error.WriteLine("Get a Gold+ subscription at https://pkgs.org/premium/");
        return 1;
    }

    using var http = new HttpClient();
    using var pkgsOrg = new PkgsOrgClient(token);

    var overview = await DistroPackagesGenerator.QueryAsync(dotnetVersion, pkgsOrg, http, Console.Error);

    // Serialize output
    string json = JsonSerializer.Serialize(overview, DistroPackagesSerializerContext.Default.DistroPackagesOverview);

    if (outputPath is not null)
    {
        await File.WriteAllTextAsync(outputPath, json);
        var info = new FileInfo(outputPath);
        Console.Error.WriteLine($"\nWrote {info.Length} bytes to {info.FullName}");
    }
    else
    {
        Console.Out.WriteLine(json);
    }

    return 0;
}

async Task<int> HandleGenerateBuildMetadataAsync(string[] args)
{
    string? repoPath = null;
    string? baseRef = null;
    string? headRef = null;
    string? outputPath = null;

    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--base" && i + 1 < args.Length)
        {
            baseRef = args[++i];
        }
        else if (args[i] == "--head" && i + 1 < args.Length)
        {
            headRef = args[++i];
        }
        else if (args[i] == "--output" && i + 1 < args.Length)
        {
            outputPath = args[++i];
        }
        else if (!args[i].StartsWith('-'))
        {
            repoPath = args[i];
        }
    }

    if (repoPath is null || baseRef is null || headRef is null)
    {
        Console.Error.WriteLine("Usage: release-notes generate build-metadata <repo-path> --base <ref> --head <ref> [--output file]");
        return 1;
    }

    using var httpClient = new HttpClient();
    var nugetClient = new NuGetFeedClient(httpClient);
    var generator = new BuildMetadataGenerator(nugetClient);

    var metadata = await generator.GenerateAsync(repoPath, baseRef, headRef);
    var json = JsonSerializer.Serialize(metadata, ChangesSerializerContext.Default.BuildMetadata);

    if (outputPath is not null)
    {
        await File.WriteAllTextAsync(outputPath, json);
        var info = new FileInfo(outputPath);
        Console.Error.WriteLine($"\nWrote {info.Length} bytes to {info.FullName}");
    }
    else
    {
        Console.Out.WriteLine(json);
    }

    return 0;
}
