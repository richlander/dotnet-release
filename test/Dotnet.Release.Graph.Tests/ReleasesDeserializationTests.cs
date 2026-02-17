using System.Text.Json;
using Dotnet.Release.Releases;
using Xunit;

namespace Dotnet.Release.Graph.Tests;

public class ReleasesDeserializationTests
{
    private static readonly HttpClient Http = new();

    [Fact]
    public async Task ReleasesIndex_Deserializes()
    {
        var url = $"{TestConstants.BaseUrl}releases-index.json";
        var json = await Http.GetStringAsync(url);
        var index = JsonSerializer.Deserialize<MajorReleasesIndex>(json, SerializerOptions.KebabCase);
        Assert.NotNull(index);
        Assert.True(index.ReleasesIndex.Count > 0);
        Assert.Contains(index.ReleasesIndex, r => r.ChannelVersion == "9.0");
    }
}
