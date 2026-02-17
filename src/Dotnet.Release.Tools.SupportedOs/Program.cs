using System.Text.Json;
using Dotnet.Release;
using Dotnet.Release.Support;
using Dotnet.Release.Tools.SupportedOs;

// Usage: dotnet-supported-os <version> [path-or-url]
// Examples:
//   dotnet-supported-os 9.0
//   dotnet-supported-os 9.0 ~/git/core/release-notes
//   dotnet-supported-os 9.0 https://raw.githubusercontent.com/dotnet/core/release-index/release-notes/

if (args.Length == 0 || !decimal.TryParse(args[0], out _))
{
    Console.Error.WriteLine("Usage: dotnet-supported-os <version> [path-or-url]");
    Console.Error.WriteLine("Examples:");
    Console.Error.WriteLine("  dotnet-supported-os 10.0");
    Console.Error.WriteLine("  dotnet-supported-os 10.0 ~/git/core/release-notes");
    Console.Error.WriteLine("  dotnet-supported-os 10.0 https://builds.dotnet.microsoft.com/dotnet/release-metadata/");
    return 1;
}

string version = args[0];
string basePath = args.Length > 1
    ? args[1]
    : "https://raw.githubusercontent.com/dotnet/core/release-index/release-notes/";

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

await SupportedOsGenerator.GenerateAsync(matrix, output, version, client);

if (outputPath is not null)
{
    await output.DisposeAsync();
    var info = new FileInfo(outputPath);
    Console.Error.WriteLine($"Generated {info.Length} bytes");
    Console.Error.WriteLine(info.FullName);
}

return 0;
