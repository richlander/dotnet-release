using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Changes;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("A set of changes shipped in a .NET release, with associated commit metadata.")]
public record ChangeRecords(
    [property: Description("Release version, e.g. \"8.0.23\" or \"11.0.0-preview.3\".")]
    string ReleaseVersion,

    [property: Description("ISO 8601 release date, e.g. \"2026-01-13\".")]
    string ReleaseDate,

    [property: Description("The change entries.")]
    IList<ChangeEntry> Changes,

    [property: Description("Normalized commit metadata, keyed by repo@shortcommit.")]
    IDictionary<string, CommitEntry> Commits
);

[Description("An externally visible change artifact (PR or commit-backed security change).")]
public record ChangeEntry(
    [property: Description("Globally unique change identifier (commit key, e.g. \"runtime@c5d5be4\").")]
    string Id,

    [property: Description("Short repository name, e.g. \"runtime\".")]
    string Repo,

    [property: Description("Change title; empty string if not available.")]
    string Title,

    [property: Description("Public GitHub PR URL; empty string if PR is non-public.")]
    string Url,

    [property: Description("Key into the top-level commits dictionary (dotnet/dotnet VMR commit).")]
    string Commit,

    [property: Description("True if this is a security change.")]
    bool IsSecurity,

    [property: Description("Product slug from products.json taxonomy."),
        JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Product = null,

    [property: Description("NuGet package name using official casing."),
        JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Package = null,

    [property: Description("CVE ID if this is a security change."),
        JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CveId = null,

    [property: Description("Key into the top-level commits dictionary (source-repo commit, e.g. runtime, aspnetcore)."),
        JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? LocalRepoCommit = null,

    [property: Description("Internal staging field for VMR commit mapping; never serialized."),
        JsonIgnore]
    string? DotnetCommit = null,

    [property: Description("GitHub PR labels from the child repo."),
        JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IList<string>? Labels = null
);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("Information about a commit associated with a change.")]
public record CommitEntry(
    [property: Description("Short repository name, e.g. \"runtime\".")]
    string Repo,

    [property: Description("Branch the commit landed on, e.g. \"release/8.0\".")]
    string Branch,

    [property: Description("Full 40-character commit hash.")]
    string Hash,

    [property: Description("GitHub organization, e.g. \"dotnet\".")]
    string Org,

    [property: Description(".diff-form commit URL for machine consumption.")]
    string Url
);
