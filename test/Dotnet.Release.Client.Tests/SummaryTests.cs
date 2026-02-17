using Dotnet.Release.Client;
using Xunit;

namespace Dotnet.Release.Client.Tests;

public class SummaryTests
{
    private static readonly HttpClient _client = new();

    private ReleaseNotesGraph CreateGraph() => new(_client);

    [Fact]
    public async Task ReleasesSummary_GetAllVersions()
    {
        var summary = CreateGraph().GetReleasesSummary();
        var versions = await summary.GetAllVersionsAsync();

        Assert.NotEmpty(versions);
    }

    [Fact]
    public async Task ReleasesSummary_GetSupportedVersions()
    {
        var summary = CreateGraph().GetReleasesSummary();
        var supported = await summary.GetSupportedVersionsAsync();

        Assert.NotEmpty(supported);
        Assert.All(supported, r => Assert.True(r.IsSupported));
    }

    [Fact]
    public async Task ReleasesSummary_GetLatestLts()
    {
        var summary = CreateGraph().GetReleasesSummary();
        var latest = await summary.GetLatestLtsAsync();

        Assert.NotNull(latest);
        Assert.True(latest.IsLts);
    }

    [Fact]
    public async Task ReleaseNavigator_GetAllPatches()
    {
        var nav = CreateGraph().GetReleaseNavigator("9.0");
        var patches = await nav.GetAllPatchesAsync();

        Assert.NotEmpty(patches);
    }

    [Fact]
    public async Task ReleaseNavigator_GetSecurityPatches()
    {
        var nav = CreateGraph().GetReleaseNavigator("9.0");
        var secPatches = await nav.GetSecurityPatchesAsync();

        Assert.All(secPatches, p => Assert.True(p.IsSecurityUpdate));
    }

    [Fact]
    public async Task ArchivesSummary_GetAllYears()
    {
        var summary = CreateGraph().GetArchivesSummary();
        var years = await summary.GetAllYearsAsync();

        Assert.NotEmpty(years);
    }

    [Fact]
    public async Task ArchiveNavigator_GetMonths()
    {
        var nav = CreateGraph().GetArchiveNavigator("2025");
        var months = await nav.GetAllMonthsAsync();

        Assert.NotEmpty(months);
    }
}
