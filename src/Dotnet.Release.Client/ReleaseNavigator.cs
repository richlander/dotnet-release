using Dotnet.Release.Graph;

namespace Dotnet.Release.Client;

/// <summary>
/// Deep navigation into a specific .NET major version.
/// Data is fetched lazily and cached automatically.
/// </summary>
public class ReleaseNavigator
{
    private readonly ReleaseNotesGraph _graph;

    public ReleaseNavigator(ReleaseNotesGraph graph, string version)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(version);
        _graph = graph;
        Version = version;
    }

    public string Version { get; }

    public async Task<PatchReleaseVersionIndex> GetPatchIndexAsync(CancellationToken cancellationToken = default)
        => await _graph.GetPatchReleaseIndexAsync(Version, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to load patch index for version {Version}");

    public async Task<ReleaseManifest> GetManifestAsync(CancellationToken cancellationToken = default)
        => await _graph.GetManifestAsync(Version, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to load manifest for version {Version}");

    public async Task<IEnumerable<PatchSummary>> GetAllPatchesAsync(CancellationToken cancellationToken = default)
    {
        var index = await GetPatchIndexAsync(cancellationToken);
        return index.Embedded?.Patches?.Select(r => new PatchSummary(r))
            ?? Enumerable.Empty<PatchSummary>();
    }

    public async Task<PatchSummary?> GetLatestPatchAsync(CancellationToken cancellationToken = default)
        => (await GetAllPatchesAsync(cancellationToken)).FirstOrDefault();

    public async Task<PatchSummary?> GetPatchAsync(string patchVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(patchVersion);
        return (await GetAllPatchesAsync(cancellationToken)).FirstOrDefault(p => p.Version == patchVersion);
    }

    public async Task<IEnumerable<PatchSummary>> GetSecurityPatchesAsync(CancellationToken cancellationToken = default)
        => (await GetAllPatchesAsync(cancellationToken)).Where(p => p.IsSecurityUpdate);

    public async Task<bool> HasSecurityUpdatesAsync(CancellationToken cancellationToken = default)
        => (await GetSecurityPatchesAsync(cancellationToken)).Any();
}
