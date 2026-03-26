using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

/// <summary>
/// Usage links pointing to documentation and help resources.
/// </summary>
[Description("Usage links for documentation and help resources")]
public record UsageLinks
{
    [JsonPropertyName("_links"),
     Description("HAL+JSON links for usage-related resources")]
    public Dictionary<string, HalLink> Links { get; set; } = new();
}

/// <summary>
/// Usage structure that combines terminology definitions with related navigation links.
/// </summary>
[Description("Usage information containing term definitions and related navigation links")]
public record UsageWithLinks
{
    [JsonPropertyName("_links"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("HAL+JSON links for usage-related resources")]
    public Dictionary<string, HalLink>? Links { get; set; }

    [Description("Term definitions (key-value pairs where key is the term and value is the definition)")]
    public Dictionary<string, string> Glossary { get; set; } = new();
}

/// <summary>
/// Base interface for .NET release version indexes.
/// </summary>
public interface IReleaseVersionIndex
{
    ReleaseKind Kind { get; }
    string Title { get; }
    string? Description { get; }
    Dictionary<string, HalLink> Links { get; }
    UsageWithLinks? Usage { get; set; }
}

[JsonConverter(typeof(KebabCaseLowerStringEnumConverter<ReleaseKind>))]
[Description("Identifies the type of release or index document")]
public enum ReleaseKind
{
    [Description("Root index of all .NET releases")]
    Root,

    [Description("Index of patches within a major .NET version")]
    Major,

    [Description("Index for a specific patch release with details")]
    Patch,

    [Description("SDK index for a major version")]
    Sdk,

    [Description("Release metadata document (manifest.json)")]
    Manifest,

    [Description("SDK feature band content")]
    Band,

    [Description("SDK download information for a feature band")]
    SdkDownload,

    [Description("Downloads index for a major version")]
    Downloads,

    [Description("Component download information (runtime, aspnetcore, windowsdesktop, sdk)")]
    ComponentDownload,

    [Description("AI-optimized index with latest patches and security status")]
    Llms,

    [Description("General content document")]
    Content,

    [Description("Unspecified type")]
    Unknown
}

/// <summary>
/// Legacy type for backward compatibility - prefer MajorReleaseVersionIndex or PatchReleaseVersionIndex
/// </summary>
[Description("Legacy index type - use MajorReleaseVersionIndex or PatchReleaseVersionIndex instead")]
public record ReleaseVersionIndex(
    [Description("Type of release document")]
    ReleaseKind Kind,
    [Description("Concise title for the document")]
    string Title,
    [Description("Description of the index scope")]
    string Description,
    [property: JsonPropertyName("_links"),
     Description("HAL+JSON links for hypermedia navigation")]
    Dictionary<string, HalLink> Links) : IReleaseVersionIndex
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Usage information and term definitions")]
    public UsageWithLinks? Usage { get; set; }

    [JsonPropertyName("_embedded"),
     JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Embedded release entries for this index level")]
    public ReleaseVersionIndexEmbedded? Embedded { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Lifecycle information")]
    public Lifecycle? Lifecycle { get; set; }
}

[Description("Container for embedded release entries in a version index (legacy)")]
public record ReleaseVersionIndexEmbedded(
    [Description("List of release entries")]
    List<ReleaseVersionIndexEntry> Releases);

[Description("Individual release entry within a version index (legacy)")]
public record ReleaseVersionIndexEntry(
    [Description("Version identifier")]
    string Version,
    [property: JsonPropertyName("_links"),
     Description("HAL+JSON links for navigation")]
    Dictionary<string, HalLink> Links)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Lifecycle information (phase and release-date)")]
    public PatchLifecycle? Lifecycle { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("CVE IDs associated with this release")]
    public IReadOnlyList<string>? CveRecords { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("SDK versions included in this patch release")]
    public IReadOnlyList<string>? SdkVersions { get; set; }
}
