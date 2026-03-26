using System.Text.Json;
using Dotnet.Release;
using Dotnet.Release.Releases;
using Dotnet.Release.Support;
using Dotnet.Release.Tools;

// Usage: dotnet-release generate <type> <version> [path-or-url] [--template <file>]
//        dotnet-release generate <type> --export-template
//        dotnet-release verify <type> <version> [path-or-url]
//        dotnet-release query distro-packages --dotnet-version <ver> [--output <file>]
// Types: supported-os, os-packages

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

// Types that don't require a version number
if (type is "releases-index" or "releases")
{
    string genPath = ".";
    string? genTemplatePath = null;

    for (int i = 2; i < args.Length; i++)
    {
        if (args[i] == "--template" && i + 1 < args.Length)
        {
            genTemplatePath = args[++i];
        }
        else if (!args[i].StartsWith('-'))
        {
            genPath = args[i];
        }
    }

    return type switch
    {
        "releases-index" => await GenerateReleasesIndexAsync(genPath),
        "releases" => await GenerateReleasesAsync(genPath, genTemplatePath),
        _ => 1
    };
}

if (args.Length < 3 || !decimal.TryParse(args[2], out _))
{
    PrintUsage();
    return 1;
}

string version = args[2];
string basePath = "https://raw.githubusercontent.com/dotnet/core/main/release-notes/";
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
    string jsonPath = path.Combine(version, "supported-os.json");
    Console.Error.WriteLine($"Reading {jsonPath}...");

    using var stream = await path.GetStreamAsync(jsonPath);
    var matrix = await JsonSerializer.DeserializeAsync(stream, SupportedOSMatrixSerializerContext.Default.SupportedOSMatrix)
        ?? throw new InvalidOperationException("Failed to deserialize supported-os.json");

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

    await SupportedOsGenerator.GenerateAsync(matrix, output, version, client, templatePath: templatePath);

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

async Task<int> VerifyReleasesAsync(string basePath, string? version, bool skipHash)
{
    string scope = version is not null ? $".NET {version}" : "all supported versions";
    Console.Error.WriteLine($"Verifying release links for {scope} in {Path.GetFullPath(basePath)}...");

    using var client = new HttpClient();
    client.DefaultRequestHeaders.UserAgent.ParseAdd("dotnet-release-verifier/1.0");
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

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: dotnet-release generate <type> <version> [path-or-url] [--template <file>]");
    Console.Error.WriteLine("       dotnet-release generate releases-index [path]");
    Console.Error.WriteLine("       dotnet-release generate releases [path] [--template <file>]");
    Console.Error.WriteLine("       dotnet-release generate <type> --export-template");
    Console.Error.WriteLine("       dotnet-release verify <type> <version> [path-or-url]");
    Console.Error.WriteLine("       dotnet-release verify releases [version] [path] [--skip-hash]");
    Console.Error.WriteLine("       dotnet-release query distro-packages --dotnet-version <ver> [--output <file>]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Types: supported-os, os-packages, dotnet-dependencies, releases-index, releases");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Examples:");
    Console.Error.WriteLine("  dotnet-release generate supported-os 10.0");
    Console.Error.WriteLine("  dotnet-release generate os-packages 10.0");
    Console.Error.WriteLine("  dotnet-release generate dotnet-dependencies 11.0");
    Console.Error.WriteLine("  dotnet-release generate dotnet-dependencies 11.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-release generate supported-os 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-release generate os-packages --export-template > my-template.md");
    Console.Error.WriteLine("  dotnet-release generate releases-index ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-release generate releases ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-release generate releases --export-template > my-template.md");
    Console.Error.WriteLine("  dotnet-release verify supported-os 10.0");
    Console.Error.WriteLine("  dotnet-release verify supported-os 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-release verify os-packages 10.0");
    Console.Error.WriteLine("  dotnet-release verify os-packages 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-release verify releases ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-release verify releases 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-release verify releases 10.0.5 ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-release verify releases ~/git/core/release-notes --skip-hash");
    Console.Error.WriteLine("  dotnet-release query distro-packages --dotnet-version 9.0");
    Console.Error.WriteLine("  dotnet-release query distro-packages --dotnet-version 9.0 --output distro-packages.json");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Environment variables:");
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
    string basePath = args.Length > 3 && !args[3].StartsWith('-') ? args[3] : "https://raw.githubusercontent.com/dotnet/core/main/release-notes/";

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
