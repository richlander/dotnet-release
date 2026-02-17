using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

[Description("Contains comprehensive metadata about a specific .NET major release")]
public record ReleaseManifest(
    [Description("Type of release document, always 'manifest'")]
    ReleaseKind Kind,
    [Description("Concise title for the document")]
    string Title,
    [Description("Major version identifier (e.g., '8.0')")]
    string Version,
    [Description("Human-friendly version label (e.g., '.NET 8.0')")]
    string Label)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Target framework moniker for this version")]
    public string? TargetFramework { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release type: lts or sts")]
    public ReleaseType? ReleaseType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Current support phase")]
    public SupportPhase? SupportPhase { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Whether this version is currently supported")]
    public bool? Supported { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("General Availability date")]
    public DateTimeOffset? GaDate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("End of Life date")]
    public DateTimeOffset? EolDate { get; init; }

    [JsonPropertyName("_links"),
     Description("HAL+JSON links for hypermedia navigation")]
    public Dictionary<string, HalLink> Links { get; init; } = [];
}
