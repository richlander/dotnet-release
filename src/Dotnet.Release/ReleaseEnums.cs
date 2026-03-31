using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release;

[JsonConverter(typeof(KebabCaseLowerStringEnumConverter<SupportPhase>))]
[Description("Support lifecycle phase for a .NET release")]
public enum SupportPhase
{
    [Description("Pre-release version available for early testing")]
    Preview,

    [Description("Pre-release version with production support commitment")]
    GoLive,

    [Description("Generally available with full support")]
    Active,

    [Description("Supported with only critical fixes (security and reliability)")]
    Maintenance,

    [Description("End of life — no longer supported")]
    Eol
}

[JsonConverter(typeof(KebabCaseLowerStringEnumConverter<ReleaseType>))]
[Description("Support duration model for a .NET release")]
public enum ReleaseType
{
    [Description("Long-Term Support — 3 years of support")]
    LTS,

    [Description("Standard-Term Support — 18 months of support")]
    STS
}

[Description("Full lifecycle information for a major .NET release")]
public record Lifecycle(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull),
     Description("Support duration model (LTS or STS), null for feature bands")]
    ReleaseType? ReleaseType,

    [property: Description("Current support phase")]
    SupportPhase Phase,

    [property: Description("General Availability date")]
    DateTimeOffset GaDate,

    [property: Description("End of Life date")]
    DateTimeOffset EolDate)
{
    [Description("Whether this release is currently supported (based on EOL date and lifecycle phase)")]
    public bool Supported { get; set; } = false;
}

[Description("Simplified lifecycle for patch releases")]
public record PatchLifecycle(
    [property: Description("Support phase at time of release")]
    SupportPhase Phase,

    [property: Description("Release date")]
    DateTimeOffset GaDate);

[JsonConverter(typeof(KebabCaseLowerStringEnumConverter<ProductComponent>))]
[Description("Major product component")]
public enum ProductComponent
{
    [Description(".NET Runtime")]
    Runtime,

    [Description(".NET SDK")]
    SDK
}

public static class ReleaseDisplayNames
{
    public static string ToDisplayName(this SupportPhase phase) => phase switch
    {
        SupportPhase.Preview => "Preview",
        SupportPhase.GoLive => "Go Live",
        SupportPhase.Active => "Active",
        SupportPhase.Maintenance => "Maintenance",
        SupportPhase.Eol => "End of Life",
        _ => phase.ToString()
    };

    public static string ToDisplayName(this ReleaseType type) => type switch
    {
        ReleaseType.LTS => "LTS",
        ReleaseType.STS => "STS",
        _ => type.ToString()
    };
}

public static class ReleaseStability
{
    public static bool IsStable(SupportPhase phase) => phase is SupportPhase.Active or SupportPhase.Maintenance;

    public static bool IsStable(Lifecycle? lifecycle) => lifecycle != null && IsStable(lifecycle.Phase);

    public static bool IsSupported(SupportPhase phase) => phase is not SupportPhase.Eol;

    public static bool IsSupported(Lifecycle? lifecycle, DateTimeOffset? referenceDate = null)
    {
        if (lifecycle == null)
            return false;

        var checkDate = referenceDate ?? DateTimeOffset.UtcNow;
        return IsStable(lifecycle.Phase) && checkDate < lifecycle.EolDate;
    }

    public static bool IsPreRelease(SupportPhase phase) => phase is SupportPhase.Preview or SupportPhase.GoLive;

    public static string? FindLatestVersion(
        IEnumerable<(string Version, Lifecycle? Lifecycle)> releases,
        StringComparer comparer)
    {
        return releases
            .Where(r => IsStable(r.Lifecycle))
            .OrderByDescending(r => r.Version, comparer)
            .Select(r => r.Version)
            .FirstOrDefault();
    }

    public static string? FindLatestLtsVersion(
        IEnumerable<(string Version, Lifecycle? Lifecycle)> releases,
        StringComparer comparer)
    {
        return releases
            .Where(r => r.Lifecycle != null &&
                       IsStable(r.Lifecycle) &&
                       r.Lifecycle.ReleaseType == ReleaseType.LTS)
            .OrderByDescending(r => r.Version, comparer)
            .Select(r => r.Version)
            .FirstOrDefault();
    }

    public static SupportPhase DeterminePhaseFromVersion(string patchVersion)
    {
        if (string.IsNullOrEmpty(patchVersion))
        {
            return SupportPhase.Preview;
        }

        var lowerVersion = patchVersion.ToLowerInvariant();

        if (lowerVersion.Contains("preview"))
        {
            return SupportPhase.Preview;
        }

        if (lowerVersion.Contains("-rc"))
        {
            return SupportPhase.GoLive;
        }

        return SupportPhase.Active;
    }

    public static SupportPhase ComputeEffectivePhase(SupportPhase phase, DateTimeOffset gaDate, DateTimeOffset? referenceDate = null)
    {
        var checkDate = referenceDate ?? DateTimeOffset.UtcNow;

        if ((phase == SupportPhase.Preview || phase == SupportPhase.GoLive) && gaDate <= checkDate)
        {
            return SupportPhase.Active;
        }

        if (phase == SupportPhase.Active && gaDate > checkDate)
        {
            return SupportPhase.Preview;
        }

        return phase;
    }
}
