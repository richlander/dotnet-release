using System.Text.Json;
using Dotnet.Release;
using Dotnet.Release.Support;
using Dotnet.Release.Tools.SupportedOs;

// Usage: dotnet-supported-os <version> [path-or-url] [--template <file>]
//        dotnet-supported-os --export-template
// Examples:
//   dotnet-supported-os 10.0
//   dotnet-supported-os 10.0 ~/git/core/release-notes
//   dotnet-supported-os --export-template > my-template.md
//   dotnet-supported-os 10.0 --template my-template.md

// Handle --export-template
if (args.Length > 0 && args[0] == "--export-template")
{
    SupportedOsGenerator.ExportTemplate(Console.Out);
    return 0;
}

if (args.Length == 0 || !decimal.TryParse(args[0], out _))
{
    Console.Error.WriteLine("Usage: dotnet-supported-os <version> [path-or-url] [--template <file>]");
    Console.Error.WriteLine("       dotnet-supported-os --export-template");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Examples:");
    Console.Error.WriteLine("  dotnet-supported-os 10.0");
    Console.Error.WriteLine("  dotnet-supported-os 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-supported-os 10.0 https://builds.dotnet.microsoft.com/dotnet/release-metadata/");
    Console.Error.WriteLine("  dotnet-supported-os --export-template > my-template.md");
    Console.Error.WriteLine("  dotnet-supported-os 10.0 --template my-template.md");
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

// Load supported-os.json
string jsonPath = path.Combine(version, "supported-os.json");
Console.Error.WriteLine($"Reading {jsonPath}...");

using var stream = await path.GetStreamAsync(jsonPath);
var matrix = await JsonSerializer.DeserializeAsync(stream, SupportedOSMatrixSerializerContext.Default.SupportedOSMatrix)
    ?? throw new InvalidOperationException("Failed to deserialize supported-os.json");

// Determine output: write to file alongside JSON when local, stdout otherwise
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
