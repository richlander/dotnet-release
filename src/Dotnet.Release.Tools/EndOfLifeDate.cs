using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Dotnet.Release.Tools;

/// <summary>
/// Client for the endoflife.date API.
/// </summary>
public static class EndOfLifeDate
{
    private const string BaseUrl = "https://endoflife.date/api/";

    /// <summary>
    /// Gets all support cycles for a product (e.g. "ubuntu", "fedora").
    /// </summary>
    public static Task<IList<SupportCycle>?> GetProductAsync(HttpClient client, string product)
        => client.GetFromJsonAsync($"{BaseUrl}{product}.json", EolSerializerContext.Default.IListSupportCycle);

    /// <summary>
    /// Gets a single support cycle for a product version.
    /// </summary>
    public static Task<SupportCycle?> GetProductCycleAsync(HttpClient client, string product, string cycle)
        => client.GetFromJsonAsync($"{BaseUrl}{product}/{cycle}.json", EolSerializerContext.Default.SupportCycle);
}

public record SupportCycle(
    string? Cycle = null,
    string? Codename = null,
    DateOnly ReleaseDate = default,
    string? Link = null)
{
    [JsonConverter(typeof(EolStringConverter))]
    public string? Eol { get; set; }

    [JsonConverter(typeof(EolStringConverter))]
    public string? Lts { get; set; }

    public string? LatestReleaseDate { get; set; }

    public SupportInfo GetSupportInfo()
    {
        if (Eol is "False")
            return new(true, DateOnly.MaxValue);

        if (Eol is not null && DateOnly.TryParse(Eol, out DateOnly eolDate))
            return new(eolDate > DateOnly.FromDateTime(DateTime.UtcNow), eolDate);

        return new(false, DateOnly.MinValue);
    }
}

public record struct SupportInfo(bool IsActive, DateOnly EolDate);

/// <summary>
/// Handles endoflife.date's eol/lts fields which can be bool or string.
/// </summary>
public class EolStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.True => "True",
        JsonTokenType.False => "False",
        _ => reader.GetString()
    };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SupportCycle))]
[JsonSerializable(typeof(IList<SupportCycle>))]
internal partial class EolSerializerContext : JsonSerializerContext
{
}
