using System.Text.Json;
using Dotnet.Release.Support;
using Xunit;

namespace Dotnet.Release.Graph.Tests;

public class SupportDeserializationTests
{
    private static readonly string CoreRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "git", "core", "release-notes");

    [Fact]
    public void SupportedOSMatrix_Deserializes()
    {
        var path = Path.Combine(CoreRoot, "9.0", "supported-os.json");
        Assert.True(File.Exists(path), $"File not found: {path}");

        var json = File.ReadAllText(path);
        var matrix = JsonSerializer.Deserialize<SupportedOSMatrix>(json, SerializerOptions.KebabCase);
        Assert.NotNull(matrix);
        Assert.Equal("9.0", matrix.ChannelVersion);
        Assert.True(matrix.Families.Count > 0);
        Assert.Contains(matrix.Families, f => f.Name == "Linux");
    }

    [Fact]
    public void OSPackages_Deserializes()
    {
        var path = Path.Combine(CoreRoot, "9.0", "os-packages.json");
        Assert.True(File.Exists(path), $"File not found: {path}");

        var json = File.ReadAllText(path);
        var packages = JsonSerializer.Deserialize<OSPackagesOverview>(json, SerializerOptions.KebabCase);
        Assert.NotNull(packages);
        Assert.Equal("9.0", packages.ChannelVersion);
        Assert.True(packages.Packages.Count > 0);
        Assert.True(packages.Distributions.Count > 0);
    }
}
