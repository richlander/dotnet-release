using System.Text.Json;
using Dotnet.Release.Graph;
using Xunit;

namespace Dotnet.Release.Graph.Tests;

/// <summary>
/// Tests that deserialize real JSON files from dotnet/core release-index branch.
/// </summary>
public class GraphDeserializationTests
{
    private static readonly HttpClient Http = new();

    private static async Task<T> DeserializeAsync<T>(string relativePath)
    {
        var url = $"{TestConstants.BaseUrl}{relativePath}";
        var json = await Http.GetStringAsync(url);
        var result = JsonSerializer.Deserialize<T>(json, SerializerOptions.SnakeCase);
        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public async Task RootIndex_Deserializes()
    {
        var index = await DeserializeAsync<MajorReleaseVersionIndex>("index.json");
        Assert.Equal(ReleaseKind.Root, index.Kind);
        Assert.NotNull(index.Embedded?.Releases);
        Assert.True(index.Embedded.Releases.Count > 0);
        Assert.Contains(index.Embedded.Releases, r => r.Version == "9.0");
        Assert.NotNull(index.Links);
        Assert.True(index.Links.ContainsKey("self"));
    }

    [Fact]
    public async Task MajorVersionIndex_Deserializes()
    {
        var index = await DeserializeAsync<PatchReleaseVersionIndex>("9.0/index.json");
        Assert.Equal(ReleaseKind.Major, index.Kind);
        Assert.NotNull(index.TargetFramework);
        Assert.NotNull(index.LatestPatch);
        Assert.NotNull(index.ReleaseType);
        Assert.NotNull(index.SupportPhase);
        Assert.NotNull(index.Embedded?.Patches);
        Assert.True(index.Embedded.Patches.Count > 0);
    }

    [Fact]
    public async Task PatchDetailIndex_Deserializes()
    {
        var index = await DeserializeAsync<PatchDetailIndex>("9.0/9.0.0/index.json");
        Assert.Equal(ReleaseKind.Patch, index.Kind);
        Assert.Equal("9.0.0", index.Version);
        Assert.NotNull(index.SdkVersion);
        Assert.NotNull(index.Embedded?.Runtime);
        Assert.NotNull(index.Embedded?.Sdk);
    }

    [Fact]
    public async Task Manifest_Deserializes()
    {
        var manifest = await DeserializeAsync<ReleaseManifest>("9.0/manifest.json");
        Assert.Equal(ReleaseKind.Manifest, manifest.Kind);
        Assert.Equal("9.0", manifest.Version);
        Assert.NotNull(manifest.TargetFramework);
        Assert.NotNull(manifest.ReleaseType);
        Assert.True(manifest.Links.ContainsKey("self"));
    }

    [Fact]
    public async Task TimelineIndex_Deserializes()
    {
        var index = await DeserializeAsync<ReleaseHistoryIndex>("timeline/index.json");
        Assert.Equal(HistoryKind.Timeline, index.Kind);
        Assert.NotNull(index.Embedded?.Years);
        Assert.True(index.Embedded.Years.Count > 0);
    }

    [Fact]
    public async Task YearIndex_Deserializes()
    {
        var index = await DeserializeAsync<HistoryYearIndex>("timeline/2025/index.json");
        Assert.Equal(HistoryKind.Year, index.Kind);
        Assert.Equal("2025", index.Year);
        Assert.NotNull(index.Embedded?.Months);
        Assert.True(index.Embedded.Months.Count > 0);
    }

    [Fact]
    public async Task MonthIndex_Deserializes()
    {
        var index = await DeserializeAsync<HistoryMonthIndex>("timeline/2025/10/index.json");
        Assert.Equal(HistoryKind.Month, index.Kind);
        Assert.Equal("2025", index.Year);
        Assert.Equal("10", index.Month);
        Assert.NotNull(index.Embedded?.Patches);
        Assert.True(index.Embedded.Patches.Count > 0);
    }

    [Fact]
    public async Task TargetFrameworks_Deserializes()
    {
        var tf = await DeserializeAsync<TargetFrameworksIndex>("9.0/target-frameworks.json");
        Assert.Equal("9.0", tf.Version);
        Assert.Equal("net9.0", tf.TargetFramework);
        Assert.True(tf.Frameworks.Count > 0);
        Assert.Contains(tf.Frameworks, f => f.Tfm == "net9.0");
    }
}
