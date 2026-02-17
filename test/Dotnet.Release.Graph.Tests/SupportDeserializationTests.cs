using System.Text.Json;
using Dotnet.Release.Support;
using Xunit;

namespace Dotnet.Release.Graph.Tests;

public class SupportDeserializationTests
{
    private static readonly HttpClient Http = new();

    [Fact]
    public async Task SupportedOSMatrix_Deserializes()
    {
        var url = $"{TestConstants.BaseUrl}9.0/supported-os.json";
        var json = await Http.GetStringAsync(url);
        var matrix = JsonSerializer.Deserialize<SupportedOSMatrix>(json, SerializerOptions.KebabCase);
        Assert.NotNull(matrix);
        Assert.Equal("9.0", matrix.ChannelVersion);
        Assert.True(matrix.Families.Count > 0);
        Assert.Contains(matrix.Families, f => f.Name == "Linux");
    }

    [Fact]
    public async Task OSPackages_Deserializes()
    {
        var url = $"{TestConstants.BaseUrl}9.0/os-packages.json";
        var json = await Http.GetStringAsync(url);
        var packages = JsonSerializer.Deserialize<OSPackagesOverview>(json, SerializerOptions.KebabCase);
        Assert.NotNull(packages);
        Assert.Equal("9.0", packages.ChannelVersion);
        Assert.True(packages.Packages.Count > 0);
        Assert.True(packages.Distributions.Count > 0);
    }
}
