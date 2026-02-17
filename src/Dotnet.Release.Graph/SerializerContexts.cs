using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(MajorReleaseVersionIndex))]
[JsonSerializable(typeof(PatchReleaseVersionIndex))]
[JsonSerializable(typeof(PatchDetailIndex))]
[JsonSerializable(typeof(MajorReleaseVersionIndexEntry))]
[JsonSerializable(typeof(PatchReleaseVersionIndexEntry))]
[JsonSerializable(typeof(Lifecycle))]
[JsonSerializable(typeof(PatchLifecycle))]
[JsonSerializable(typeof(CveRecordSummary))]
public partial class ReleaseVersionIndexSerializerContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(ReleaseManifest))]
public partial class ReleaseManifestSerializerContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(ReleaseHistoryIndex))]
public partial class ReleaseHistoryIndexSerializerContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(HistoryYearIndex))]
[JsonSerializable(typeof(HistoryMonthIndex))]
[JsonSerializable(typeof(HistoryMonthSummary))]
[JsonSerializable(typeof(CveRecordSummary))]
[JsonSerializable(typeof(HalLink))]
public partial class HistoryYearIndexSerializerContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(SdkVersionIndex))]
[JsonSerializable(typeof(SdkDownloadInfo))]
[JsonSerializable(typeof(SdkDownloadEmbedded))]
[JsonSerializable(typeof(SdkDownloadFile))]
public partial class SdkVersionIndexSerializerContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(DownloadsIndex))]
[JsonSerializable(typeof(ComponentDownload))]
[JsonSerializable(typeof(DownloadFile))]
public partial class DownloadsIndexSerializerContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(TargetFrameworksIndex))]
public partial class TargetFrameworksSerializerContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(LlmsIndex))]
[JsonSerializable(typeof(LlmsWorkflow))]
[JsonSerializable(typeof(HistoryMonthSummary))]
public partial class LlmsIndexSerializerContext : JsonSerializerContext
{
}
