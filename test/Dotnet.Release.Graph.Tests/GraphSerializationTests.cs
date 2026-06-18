using System.Text.Json;
using Dotnet.Release.Graph;
using Xunit;

namespace Dotnet.Release.Graph.Tests;

public class GraphSerializationTests
{
    [Fact]
    public void PatchReleaseVersionIndexEntry_SerializesSdkVersions()
    {
        var entry = new PatchReleaseVersionIndexEntry(
            "8.0.12",
            new DateTimeOffset(2025, 1, 14, 0, 0, 0, TimeSpan.Zero),
            "2025",
            "01",
            true,
            SupportPhase.Active)
        {
            SdkVersion = "8.0.405",
            SdkVersions = ["8.0.405", "8.0.308", "8.0.112"],
            Links = new Dictionary<string, HalLink>
            {
                [HalTerms.Self] = new("https://example.com/8.0/8.0.12/index.json")
            }
        };

        var json = JsonSerializer.Serialize(
            entry,
            ReleaseVersionIndexSerializerContext.Default.PatchReleaseVersionIndexEntry);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("8.0.405", root.GetProperty("sdk_version").GetString());
        var sdkVersions = root.GetProperty("sdk_versions").EnumerateArray().Select(v => v.GetString()!).ToArray();
        Assert.Equal(["8.0.405", "8.0.308", "8.0.112"], sdkVersions);
    }
}
