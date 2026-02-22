using System.Text.Json;
using Dotnet.Release;
using Dotnet.Release.Support;
using Dotnet.Release.Tools;

// Usage: dotnet-release generate <type> <version> [path-or-url] [--template <file>]
//        dotnet-release generate <type> --export-template
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
        default:
            Console.Error.WriteLine($"Unknown type: {type}");
            PrintUsage();
            return 1;
    }
    return 0;
}

if (args.Length < 3 || !decimal.TryParse(args[2], out _))
{
    PrintUsage();
    return 1;
}

string version = args[2];
string basePath = "https://raw.githubusercontent.com/dotnet/core/release-index/release-notes/";
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

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: dotnet-release generate <type> <version> [path-or-url] [--template <file>]");
    Console.Error.WriteLine("       dotnet-release generate <type> --export-template");
    Console.Error.WriteLine("       dotnet-release query distro-packages --dotnet-version <ver> [--output <file>]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Types: supported-os, os-packages");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Examples:");
    Console.Error.WriteLine("  dotnet-release generate supported-os 10.0");
    Console.Error.WriteLine("  dotnet-release generate os-packages 10.0");
    Console.Error.WriteLine("  dotnet-release generate supported-os 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-release generate os-packages --export-template > my-template.md");
    Console.Error.WriteLine("  dotnet-release generate supported-os 10.0 --template my-template.md");
    Console.Error.WriteLine("  dotnet-release query distro-packages --dotnet-version 9.0");
    Console.Error.WriteLine("  dotnet-release query distro-packages --dotnet-version 9.0 --output distro-packages.json");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Environment variables:");
    Console.Error.WriteLine("  PKGS_ORG_TOKEN  API token for pkgs.org (Gold+ subscription required)");
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
