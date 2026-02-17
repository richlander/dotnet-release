using Dotnet.Release.Graph;

namespace Dotnet.Release.Client;

/// <summary>
/// Summary of a .NET major version with support status.
/// </summary>
public class ReleaseSummary
{
    private readonly MajorReleaseVersionIndexEntry _entry;

    public ReleaseSummary(MajorReleaseVersionIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entry = entry;
    }

    public string Version => _entry.Version;
    public ReleaseType? ReleaseType => _entry.ReleaseType;
    public SupportPhase? Phase => _entry.SupportPhase;
    public DateTimeOffset? ReleaseDate => _entry.GaDate;
    public DateTimeOffset? EolDate => _entry.EolDate;
    public bool IsSupported => _entry.Supported ?? false;
    public bool IsLts => ReleaseType == Release.ReleaseType.LTS;
    public bool IsSts => ReleaseType == Release.ReleaseType.STS;
    public bool IsActive => Phase == SupportPhase.Active;
    public bool IsPreview => Phase == SupportPhase.Preview;
    public bool IsEol => Phase == SupportPhase.Eol;
    public IReadOnlyDictionary<string, HalLink>? Links => _entry.Links;
}
