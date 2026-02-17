using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

[Description("Provides chronological access to .NET releases organized by time periods")]
public record ReleaseHistoryIndex(
    [Description("Type of timeline index")]
    HistoryKind Kind,
    [Description("Concise title for the document")]
    string Title)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Context-aware description of the time period")]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest major .NET version (e.g., '10.0')")]
    public string? LatestMajor { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest LTS major .NET version")]
    public string? LatestLtsMajor { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest year with .NET releases")]
    public string? LatestYear { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest month with security releases")]
    public string? LatestSecurityMonth { get; init; }

    [JsonPropertyName("_links"),
     Description("HAL+JSON links for hypermedia navigation")]
    public Dictionary<string, HalLink> Links { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Glossary of timeline-specific terms")]
    public Dictionary<string, string>? Glossary { get; set; }

    [JsonPropertyName("_embedded"),
     Description("Embedded time-based navigation entries")]
    public ReleaseHistoryIndexEmbedded? Embedded { get; set; }
}

[Description("Container for embedded chronological navigation entries")]
public record ReleaseHistoryIndexEmbedded
{
    [Description("Yearly navigation entries")]
    public List<HistoryYearEntry>? Years { get; set; }
}

[Description("Year entry in the release history")]
public record HistoryYearEntry(
    [Description("Year identifier (e.g., '2025')")]
    string Year)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Description of the year's releases")]
    public string? Description { get; init; }

    [JsonPropertyName("major_releases"),
     Description("List of .NET major version identifiers released during this year")]
    public IList<string>? MajorReleases { get; set; }

    [JsonPropertyName("_links"),
     Description("HAL+JSON links for navigation")]
    public Dictionary<string, HalLink> Links { get; set; } = [];
}

[JsonConverter(typeof(KebabCaseLowerStringEnumConverter<HistoryKind>))]
[Description("Identifies the type of timeline index document")]
public enum HistoryKind
{
    [Description("Root chronological index")]
    Timeline,
    [Description("Year-specific index")]
    Year,
    [Description("Month-specific index")]
    Month,
    [Description("Resource manifest for a timeline entry")]
    Manifest
}
