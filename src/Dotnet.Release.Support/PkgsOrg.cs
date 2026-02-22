using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Support;

// Models for the pkgs.org API (https://pkgs.org/api/)
// Requires Gold+ subscription. Token via PKGS_ORG_TOKEN env var.

[Description("A distribution on pkgs.org.")]
public record PkgsOrgDistribution(
    [property: JsonPropertyName("id")]
    int Id,

    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonPropertyName("version")]
    string Version);

[Description("A repository on pkgs.org.")]
public record PkgsOrgRepository(
    [property: JsonPropertyName("id")]
    int Id,

    [property: JsonPropertyName("distribution_id")]
    int DistributionId,

    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonPropertyName("architecture")]
    string Architecture,

    [property: JsonPropertyName("official")]
    bool Official);

[Description("A package search result from pkgs.org.")]
public record PkgsOrgPackage(
    [property: JsonPropertyName("filename")]
    string Filename,

    [property: JsonPropertyName("filename_src")]
    string? FilenameSrc,

    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonPropertyName("epoch")]
    int? Epoch,

    [property: JsonPropertyName("version")]
    string Version,

    [property: JsonPropertyName("release")]
    string? Release,

    [property: JsonPropertyName("architecture")]
    string Architecture,

    [property: JsonPropertyName("url_binary")]
    string? UrlBinary,

    [property: JsonPropertyName("url_source")]
    string? UrlSource)
{
    [JsonPropertyName("distribution_id")]
    public int DistributionId { get; init; }

    [JsonPropertyName("repository_id")]
    public int RepositoryId { get; init; }
}

[JsonSerializable(typeof(IList<PkgsOrgDistribution>))]
[JsonSerializable(typeof(IList<PkgsOrgRepository>))]
[JsonSerializable(typeof(IList<PkgsOrgPackage>))]
public partial class PkgsOrgSerializerContext : JsonSerializerContext
{
}
