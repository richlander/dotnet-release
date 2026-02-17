using System.Text.Json;
using Dotnet.Release.Graph;
using Xunit;

namespace Dotnet.Release.Graph.Tests;

/// <summary>
/// Tests that deserialize real JSON files from ~/git/core/release-notes/.
/// </summary>
public class GraphDeserializationTests
{
    private static readonly string CoreRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "git", "core", "release-notes");

    private static T Deserialize<T>(string relativePath)
    {
        var path = Path.Combine(CoreRoot, relativePath);
        Assert.True(File.Exists(path), $"File not found: {path}");
        var json = File.ReadAllText(path);
        var result = JsonSerializer.Deserialize<T>(json, SerializerOptions.SnakeCase);
        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public void RootIndex_Deserializes()
    {
        var index = Deserialize<MajorReleaseVersionIndex>("index.json");
        Assert.Equal(ReleaseKind.Root, index.Kind);
        Assert.NotNull(index.Embedded?.Releases);
        Assert.True(index.Embedded.Releases.Count > 0);
        Assert.Contains(index.Embedded.Releases, r => r.Version == "9.0");
        Assert.NotNull(index.Links);
        Assert.True(index.Links.ContainsKey("self"));
    }

    [Fact]
    public void MajorVersionIndex_Deserializes()
    {
        var index = Deserialize<PatchReleaseVersionIndex>("9.0/index.json");
        Assert.Equal(ReleaseKind.Major, index.Kind);
        Assert.NotNull(index.TargetFramework);
        Assert.NotNull(index.LatestPatch);
        Assert.NotNull(index.ReleaseType);
        Assert.NotNull(index.SupportPhase);
        Assert.NotNull(index.Embedded?.Patches);
        Assert.True(index.Embedded.Patches.Count > 0);
    }

    [Fact]
    public void PatchDetailIndex_Deserializes()
    {
        var index = Deserialize<PatchDetailIndex>("9.0/9.0.0/index.json");
        Assert.Equal(ReleaseKind.Patch, index.Kind);
        Assert.Equal("9.0.0", index.Version);
        Assert.NotNull(index.SdkVersion);
        Assert.NotNull(index.Embedded?.Runtime);
        Assert.NotNull(index.Embedded?.Sdk);
    }

    [Fact]
    public void Manifest_Deserializes()
    {
        var manifest = Deserialize<ReleaseManifest>("9.0/manifest.json");
        Assert.Equal(ReleaseKind.Manifest, manifest.Kind);
        Assert.Equal("9.0", manifest.Version);
        Assert.NotNull(manifest.TargetFramework);
        Assert.NotNull(manifest.ReleaseType);
        Assert.True(manifest.Links.ContainsKey("self"));
    }

    [Fact]
    public void TimelineIndex_Deserializes()
    {
        var index = Deserialize<ReleaseHistoryIndex>("timeline/index.json");
        Assert.Equal(HistoryKind.Timeline, index.Kind);
        Assert.NotNull(index.Embedded?.Years);
        Assert.True(index.Embedded.Years.Count > 0);
    }

    [Fact]
    public void YearIndex_Deserializes()
    {
        var index = Deserialize<HistoryYearIndex>("timeline/2025/index.json");
        Assert.Equal(HistoryKind.Year, index.Kind);
        Assert.Equal("2025", index.Year);
        Assert.NotNull(index.Embedded?.Months);
        Assert.True(index.Embedded.Months.Count > 0);
    }

    [Fact]
    public void MonthIndex_Deserializes()
    {
        var index = Deserialize<HistoryMonthIndex>("timeline/2025/10/index.json");
        Assert.Equal(HistoryKind.Month, index.Kind);
        Assert.Equal("2025", index.Year);
        Assert.Equal("10", index.Month);
        Assert.NotNull(index.Embedded?.Patches);
        Assert.True(index.Embedded.Patches.Count > 0);
    }

    [Fact]
    public void TargetFrameworks_Deserializes()
    {
        var tf = Deserialize<TargetFrameworksIndex>("9.0/target-frameworks.json");
        Assert.Equal("9.0", tf.Version);
        Assert.Equal("net9.0", tf.TargetFramework);
        Assert.True(tf.Frameworks.Count > 0);
        Assert.Contains(tf.Frameworks, f => f.Tfm == "net9.0");
    }
}
