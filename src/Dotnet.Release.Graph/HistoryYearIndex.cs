using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

[Description("Index of .NET releases for a specific year, organized by months")]
public record HistoryYearIndex(
    [Description("Type of history index document")]
    HistoryKind Kind,
    [Description("Concise title for the document")]
    string Title,
    [Description("Year identifier (e.g., '2025')")]
    string Year)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Description of the year's releases")]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest month with .NET releases in this year")]
    public string? LatestMonth { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest month with security releases in this year")]
    public string? LatestSecurityMonth { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Latest major version with GA releases in this year")]
    public string? LatestMajor { get; init; }

    [JsonPropertyName("major_releases"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Major versions with releases in this year")]
    public IList<string>? MajorReleases { get; init; }

    [JsonPropertyName("_links"),
     Description("HAL+JSON links for hypermedia navigation")]
    public Dictionary<string, HalLink> Links { get; init; } = [];

    [JsonPropertyName("_embedded"),
     Description("Embedded monthly summaries")]
    public HistoryYearIndexEmbedded? Embedded { get; set; }
}

[Description("Container for embedded year-level navigation entries")]
public record HistoryYearIndexEmbedded
{
    [Description("Monthly release summaries for this year")]
    public List<HistoryMonthSummary>? Months { get; set; }
}

[Description("Simplified month entry for year-level summaries")]
public record HistoryMonthSummary(
    [Description("Month identifier (e.g., '02' for February)")]
    string Month,
    [Description("True if any release this month includes security fixes")]
    bool Security,
    [property: JsonPropertyName("_links"),
     Description("HAL+JSON links for navigation")]
    Dictionary<string, HalLink> Links);

[Description("Index of .NET releases for a specific month")]
public record HistoryMonthIndex(
    [Description("Type of history index document")]
    HistoryKind Kind,
    [Description("Concise title for the document")]
    string Title,
    [Description("Year identifier (e.g., '2025')")]
    string Year,
    [Description("Month identifier (e.g., '02' for February)")]
    string Month,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date for this month's patches")]
    DateTimeOffset? Date,
    [Description("True if any release this month includes security fixes")]
    bool Security)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Description of the month's releases")]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date of the previous month with releases")]
    public DateTimeOffset? PrevMonthDate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Release date of the previous month with security releases")]
    public DateTimeOffset? PrevSecurityMonthDate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("CVE identifiers disclosed this month")]
    public IList<string>? CveRecords { get; init; }

    [JsonPropertyName("_links"),
     Description("HAL+JSON links for hypermedia navigation")]
    public Dictionary<string, HalLink> Links { get; init; } = [];

    [JsonPropertyName("_embedded"),
     Description("Embedded release listings for this month")]
    public HistoryMonthIndexEmbedded? Embedded { get; set; }
}

[Description("Container for embedded month-level release entries")]
public record HistoryMonthIndexEmbedded
{
    [Description("Patch releases this month, keyed by major version")]
    public Dictionary<string, PatchReleaseVersionIndexEntry>? Patches { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("CVE security vulnerability disclosures for this month")]
    public IReadOnlyList<CveRecordSummary>? Disclosures { get; set; }
}
