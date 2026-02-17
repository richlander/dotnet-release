using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Graph;

/// <summary>
/// Target frameworks document for a major .NET version.
/// </summary>
[Description("Target frameworks supported by a major .NET version")]
public record TargetFrameworksIndex(
    [property: Description("Major version identifier (e.g., '10.0')")]
    string Version,
    [property: Description("Human-friendly name (e.g., '.NET 10')")]
    string Name,
    [property: Description("Base target framework moniker (e.g., 'net10.0')")]
    string TargetFramework)
{
    [Description("List of supported target frameworks")]
    public IReadOnlyList<TargetFrameworkEntry> Frameworks { get; init; } = [];
}

[Description("Target framework entry with platform-specific details")]
public record TargetFrameworkEntry(
    [property: Description("Target framework moniker (e.g., 'net10.0', 'net10.0-ios')")]
    string Tfm)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Canonical TFM with explicit platform version (e.g., 'net10.0-ios18.7')")]
    public string? Canonical { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Platform identifier (e.g., 'ios', 'android', 'windows')")]
    public string? Platform { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Human-readable platform name")]
    public string? PlatformName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Default platform version for this .NET release")]
    public string? PlatformVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Human-readable description of the target framework")]
    public string? Description { get; init; }
}
