using Dotnet.Release.Client;
using Dotnet.Release.Graph;
using Xunit;

namespace Dotnet.Release.Client.Tests;

public class GraphNavigationTests
{
    private static readonly HttpClient _client = new();

    private ReleaseNotesGraph CreateGraph() => new(_client);

    [Fact]
    public async Task GetMajorReleaseIndex_ReturnsVersions()
    {
        var graph = CreateGraph();
        var index = await graph.GetMajorReleaseIndexAsync();

        Assert.NotNull(index);
        Assert.NotNull(index.Embedded?.Releases);
        Assert.True(index.Embedded.Releases.Count > 0);
    }

    [Fact]
    public async Task GetPatchReleaseIndex_ReturnsPatchesFor9()
    {
        var graph = CreateGraph();
        var index = await graph.GetPatchReleaseIndexAsync("9.0");

        Assert.NotNull(index);
        Assert.NotNull(index.Embedded?.Patches);
        Assert.True(index.Embedded.Patches.Count > 0);
    }

    [Fact]
    public async Task GetManifest_ReturnsManifestFor9()
    {
        var graph = CreateGraph();
        var manifest = await graph.GetManifestAsync("9.0");

        Assert.NotNull(manifest);
        Assert.Equal("9.0", manifest.Version);
    }

    [Fact]
    public async Task GetReleaseHistoryIndex_ReturnsYears()
    {
        var graph = CreateGraph();
        var history = await graph.GetReleaseHistoryIndexAsync();

        Assert.NotNull(history);
        Assert.NotNull(history.Embedded?.Years);
        Assert.True(history.Embedded.Years.Count > 0);
    }

    [Fact]
    public async Task GetYearIndex_Returns2025()
    {
        var graph = CreateGraph();
        var year = await graph.GetYearIndexAsync("2025");

        Assert.NotNull(year);
        Assert.Equal("2025", year.Year);
    }
}
