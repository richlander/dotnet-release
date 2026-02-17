using System.Text.Json.Serialization;

namespace Dotnet.Release.Support;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.KebabCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(OSPackagesOverview))]
public partial class OSPackagesSerializerContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.KebabCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(SupportedOSMatrix))]
public partial class SupportedOSMatrixSerializerContext : JsonSerializerContext
{
}
