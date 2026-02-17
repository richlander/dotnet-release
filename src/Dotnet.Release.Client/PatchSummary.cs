using Dotnet.Release.Graph;

namespace Dotnet.Release.Client;

/// <summary>
/// Summary of a .NET patch release.
/// </summary>
public class PatchSummary
{
    private readonly PatchReleaseVersionIndexEntry _entry;

    public PatchSummary(PatchReleaseVersionIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entry = entry;
    }

    public string Version => _entry.Version;
    public SupportPhase? Phase => _entry.SupportPhase;
    public DateTimeOffset? ReleaseDate => _entry.Date;
    public bool IsSecurityUpdate => _entry.Security;
    public IReadOnlyDictionary<string, HalLink>? Links => _entry.Links;
}
