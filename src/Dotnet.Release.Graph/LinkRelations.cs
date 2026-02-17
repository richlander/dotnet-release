namespace Dotnet.Release.Graph;

/// <summary>
/// Standard link relation names for .NET release index files.
/// </summary>
public static class LinkRelations
{
    // Version-based hierarchy: Root → Major → Patch
    public const string Root = "root";
    public const string Major = "major";
    public const string Patch = "patch";

    // Timeline-based hierarchy: Timeline → Year → Month
    public const string Timeline = "timeline";
    public const string Year = "year";
    public const string Month = "month";

    // Manifest and supplementary documents
    public const string Manifest = "manifest";
    public const string MajorManifest = "major-manifest";
    public const string CveJson = "cve-json";
    public const string Release = "release";
    public const string ReleaseJson = "release-json";

    // Latest link relations
    public const string LatestMajor = "latest-major";
    public const string LatestLtsMajor = "latest-lts-major";
    public const string Downloads = "downloads";
    public const string LatestSecurityPatch = "latest-security-patch";
    public const string LatestYear = "latest-year";
    public const string LatestMonth = "latest-month";
    public const string LatestSecurityMonth = "latest-security-month";
    public const string SecurityDisclosures = "security-disclosures";
    public const string LatestSecurityDisclosures = "latest-security-disclosures";
    public const string LatestCveJson = "latest-cve-json";
    public const string LatestPatch = "latest-patch";
    public const string CompatibilityJson = "compatibility-json";
    public const string TargetFrameworksJson = "target-frameworks-json";

    // Previous link relations
    public const string PrevPatch = "prev-patch";
    public const string PrevMonth = "prev-month";
    public const string PrevYear = "prev-year";
    public const string PrevSecurityPatch = "prev-security-patch";
    public const string PrevSecurityMonth = "prev-security-month";

    // Skill and workflow resources
    public const string Workflows = "workflows";
}
