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
    [property: Description("Support duration model (LTS or STS)")]
    ReleaseType ReleaseType,

    [property: Description("Current support phase")]
    SupportPhase Phase,

    [property: Description("General Availability date")]
    DateTimeOffset GaDate,

    [property: Description("End of Life date")]
    DateTimeOffset EolDate)
{
    [Description("Whether this release is currently supported (not EOL)")]
    public bool Supported => Phase != SupportPhase.Eol;
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

public static class ReleaseStability
{
    public static bool IsStable(SupportPhase phase) => phase is SupportPhase.Active or SupportPhase.Maintenance;

    public static bool IsSupported(SupportPhase phase) => phase is not SupportPhase.Eol;

    public static bool IsPreRelease(SupportPhase phase) => phase is SupportPhase.Preview or SupportPhase.GoLive;
}
