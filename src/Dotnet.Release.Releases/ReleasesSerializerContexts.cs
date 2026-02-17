using System.Text.Json.Serialization;

namespace Dotnet.Release.Releases;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.KebabCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(MajorReleasesIndex))]
public partial class MajorReleasesIndexSerializerContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.KebabCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(MajorReleaseOverview))]
public partial class MajorReleaseOverviewSerializerContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.KebabCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(PatchReleaseOverview))]
public partial class PatchReleaseOverviewSerializerContext : JsonSerializerContext
{
}
