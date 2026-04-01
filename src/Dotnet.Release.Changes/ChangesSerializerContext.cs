using System.Text.Json.Serialization;

namespace Dotnet.Release.Changes;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(ChangeRecords))]
[JsonSerializable(typeof(BuildMetadata))]
public partial class ChangesSerializerContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SourceManifest))]
public partial class SourceManifestSerializerContext : JsonSerializerContext
{
}
