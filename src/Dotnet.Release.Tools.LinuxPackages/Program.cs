using System.Text.Json;
using Dotnet.Release;
using Dotnet.Release.Support;
using Dotnet.Release.Tools.LinuxPackages;

// Usage: dotnet-linux-packages <version> [path-or-url] [--template <file>]
//        dotnet-linux-packages --export-template
// Examples:
//   dotnet-linux-packages 10.0
//   dotnet-linux-packages 10.0 ~/git/core/release-notes
//   dotnet-linux-packages --export-template > my-template.md
//   dotnet-linux-packages 10.0 --template my-template.md

// Handle --export-template
if (args.Length > 0 && args[0] == "--export-template")
{
    LinuxPackagesGenerator.ExportTemplate(Console.Out);
    return 0;
}

if (args.Length == 0 || !decimal.TryParse(args[0], out _))
{
    Console.Error.WriteLine("Usage: dotnet-linux-packages <version> [path-or-url] [--template <file>]");
    Console.Error.WriteLine("       dotnet-linux-packages --export-template");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Examples:");
    Console.Error.WriteLine("  dotnet-linux-packages 10.0");
    Console.Error.WriteLine("  dotnet-linux-packages 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-linux-packages 10.0 https://builds.dotnet.microsoft.com/dotnet/release-metadata/");
    Console.Error.WriteLine("  dotnet-linux-packages --export-template > my-template.md");
    Console.Error.WriteLine("  dotnet-linux-packages 10.0 --template my-template.md");
    return 1;
}

string version = args[0];
string basePath = "https://raw.githubusercontent.com/dotnet/core/release-index/release-notes/";
string? templatePath = null;

// Parse remaining args
for (int i = 1; i < args.Length; i++)
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

// Load os-packages.json
string packageJsonPath = path.Combine(version, "os-packages.json");
Console.Error.WriteLine($"Reading {packageJsonPath}");

using Stream packageStream = await path.GetStreamAsync(packageJsonPath);
OSPackagesOverview overview = await JsonSerializer.DeserializeAsync(
    packageStream,
    OSPackagesSerializerContext.Default.OSPackagesOverview)
    ?? throw new InvalidOperationException("Failed to deserialize os-packages.json");

// Determine output
string? outputPath = null;
TextWriter output;

if (path.SupportsLocalPaths)
{
    outputPath = path.Combine(version, "linux-packages.md");
    output = new StreamWriter(File.Open(outputPath, FileMode.Create));
}
else
{
    output = Console.Out;
}

LinuxPackagesGenerator.Generate(overview, output, version, templatePath: templatePath);

if (outputPath is not null)
{
    await output.DisposeAsync();
    var info = new FileInfo(outputPath);
    Console.Error.WriteLine($"Generated {info.Length} bytes");
    Console.Error.WriteLine(info.FullName);
}

return 0;
