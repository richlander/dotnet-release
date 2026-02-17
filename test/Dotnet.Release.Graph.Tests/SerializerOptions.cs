using System.Text.Json;
using System.Text.Json.Serialization;
using Dotnet.Release.Graph;

namespace Dotnet.Release.Graph.Tests;

/// <summary>
/// JSON serializer options matching the HAL+JSON conventions used in dotnet/core.
/// </summary>
internal static class SerializerOptions
{
    public static JsonSerializerOptions SnakeCase { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static JsonSerializerOptions KebabCase { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
        WriteIndented = true
    };
}
