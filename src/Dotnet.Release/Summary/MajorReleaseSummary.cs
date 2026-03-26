namespace Dotnet.Release.Summary;

public record MajorReleaseSummary
(
    string MajorVersion,
    string MajorVersionLabel,
    Lifecycle Lifecycle,
    IList<SdkBand> SdkBands,
    IList<PatchReleaseSummary> PatchReleases
)
{
    public ReleaseType? ReleaseType => Lifecycle.ReleaseType;
    public SupportPhase SupportPhase => Lifecycle.Phase;
    public DateTimeOffset GaDate => Lifecycle.GaDate;
    public DateTimeOffset EolDate => Lifecycle.EolDate;
};
