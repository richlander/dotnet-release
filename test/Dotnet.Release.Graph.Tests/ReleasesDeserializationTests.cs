using System.Text.Json;
using Dotnet.Release.Releases;
using Xunit;

namespace Dotnet.Release.Graph.Tests;

public class ReleasesDeserializationTests
{
    private static readonly string CoreRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "git", "core", "release-notes");

    [Fact]
    public void ReleasesIndex_Deserializes()
    {
        var path = Path.Combine(CoreRoot, "releases-index.json");
        Assert.True(File.Exists(path), $"File not found: {path}");

        var json = File.ReadAllText(path);
        var index = JsonSerializer.Deserialize<MajorReleasesIndex>(json, SerializerOptions.KebabCase);
        Assert.NotNull(index);
        Assert.True(index.ReleasesIndex.Count > 0);
        Assert.Contains(index.ReleasesIndex, r => r.ChannelVersion == "9.0");
    }
}
