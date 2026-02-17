using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

[Description("HAL+JSON hypermedia link providing navigation to related resources")]
public record HalLink(
    [Description("Absolute URL to the linked resource")]
    string Href)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Descriptive title for the linked resource")]
    public string? Title { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("MIME type of the linked resource. Omit for HAL+JSON (default).")]
    public string? Type { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Indicates that the href is a URI template (RFC 6570)")]
    public bool? Templated { get; set; }
}

public class HalTerms
{
    public const string Self = "self";
    public const string Index = "index";
    public const string Releases = "releases";
    public const string Manifest = "manifest";
    public const string ReleaseInfo = "release-info";
    public const string PatchReleasesIndex = "patch-releases-index";
    public const string PatchRelease = "patch-release";
    public const string Next = "next";
    public const string Prev = "prev";
}

public class MediaType
{
    public const string Markdown = "application/markdown";
    public const string Json = "application/json";
    public const string HalJson = "application/hal+json";
    public const string Text = "text/plain";
    public const string Html = "text/html";
}

[Description("Metadata about when and how this document was generated")]
public record GenerationMetadata(
    [Description("Version of the schema used for this document")]
    string SchemaVersion,
    [Description("ISO 8601 timestamp when this document was generated")]
    DateTimeOffset GeneratedOn,
    [Description("Name of the tool or script that generated this document")]
    string GeneratedBy);
