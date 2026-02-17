using System.Text.Json;
using Dotnet.Release.Cve;
using Xunit;

namespace Dotnet.Release.Graph.Tests;

public class CveDeserializationTests
{
    private static readonly string CoreRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "git", "core", "release-notes");

    [Fact]
    public void CveRecords_Deserializes()
    {
        var path = Path.Combine(CoreRoot, "timeline", "2025", "10", "cve.json");
        Assert.True(File.Exists(path), $"File not found: {path}");

        var json = File.ReadAllText(path);
        var records = JsonSerializer.Deserialize<CveRecords>(json, SerializerOptions.SnakeCase);
        Assert.NotNull(records);
        Assert.NotNull(records.Title);
        Assert.True(records.Disclosures.Count > 0);
        Assert.True(records.Products.Count > 0);

        var firstCve = records.Disclosures[0];
        Assert.NotNull(firstCve.Id);
        Assert.NotNull(firstCve.Problem);
        Assert.NotNull(firstCve.Cvss);
        Assert.True(firstCve.Cvss.Score > 0);
    }
}
