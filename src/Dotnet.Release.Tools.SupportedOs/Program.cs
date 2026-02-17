using System.Text.Json;
using Dotnet.Release.Support;
using Dotnet.Release.Tools.SupportedOs;

// Usage: dotnet run -- <version> [url-or-path]
// Example: dotnet run -- 9.0
//          dotnet run -- 9.0 https://raw.githubusercontent.com/dotnet/core/release-index/release-notes/

if (args.Length == 0 || !decimal.TryParse(args[0], out _))
{
    Console.Error.WriteLine("Usage: Dotnet.Release.Tools.SupportedOs <version> [base-url]");
    Console.Error.WriteLine("Example: dotnet run -- 9.0");
    return 1;
}

string version = args[0];
string baseUrl = args.Length > 1
    ? args[1]
    : "https://raw.githubusercontent.com/dotnet/core/release-index/release-notes/";

if (!baseUrl.EndsWith('/')) baseUrl += '/';

string jsonUrl = $"{baseUrl}{version}/supported-os.json";

Console.Error.WriteLine($"Fetching {jsonUrl}...");

using var client = new HttpClient();
using var stream = await client.GetStreamAsync(jsonUrl);
var matrix = await JsonSerializer.DeserializeAsync(stream, SupportedOSMatrixSerializerContext.Default.SupportedOSMatrix)
    ?? throw new InvalidOperationException("Failed to deserialize supported-os.json");

SupportedOsGenerator.Generate(matrix, Console.Out, version, supportPhase: "Active", releaseType: "STS");

return 0;
