using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Changes;

[Description("Build metadata for API verification against nightly packages.")]
public record BuildMetadata(
    [property: Description("Release version, e.g. \"11.0.0-preview.3\".")]
    string Version,

    [property: Description("Base VMR ref (previous release tag or branch).")]
    string BaseRef,

    [property: Description("Head VMR ref (target release branch or tag).")]
    string HeadRef,

    [property: Description("Build version information.")]
    BuildInfo Build,

    [property: Description("NuGet feed and package information for API verification.")]
    NuGetInfo NuGet
);

[Description("Nightly build version details.")]
public record BuildInfo(
    [property: Description("Runtime build version, e.g. \"11.0.0-preview.3.26179.102\".")]
    string Version,

    [property: Description("SDK build version, e.g. \"11.0.100-preview.3.26179.102\".")]
    string SdkVersion,

    [property: Description("SDK tarball URL template with {platform} placeholder.")]
    string SdkUrl
);

[Description("NuGet feed and ref pack packages for API inspection.")]
public record NuGetInfo(
    [property: Description("NuGet v3 feed URL for nightly packages.")]
    string Source,

    [property: Description("Ref pack package names and their latest build versions.")]
    IDictionary<string, string> Packages
);
