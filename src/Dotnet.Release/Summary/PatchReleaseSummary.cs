using Dotnet.Release.Releases;

namespace Dotnet.Release.Summary;

public record PatchReleaseSummary
(
    string MajorVersion,
    string PatchVersion,
    DateOnly ReleaseDate,
    bool Security,
    IList<Releases.Cve> CveList,
    IList<ReleaseComponent> Components
)
{
    public string? ReleaseJsonPath { get; set; }
    public string? PatchDirPath { get; set; }
}

public record ReleaseComponent
(
    string Name,
    string Version,
    string Label
);
