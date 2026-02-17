using Dotnet.Release.Graph;

namespace Dotnet.Release.Client;

/// <summary>
/// High-level summary of all .NET releases with support status.
/// </summary>
public class ReleasesSummary
{
    private readonly ReleaseNotesGraph _graph;

    public ReleasesSummary(ReleaseNotesGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
    }

    public async Task<IEnumerable<ReleaseSummary>> GetAllVersionsAsync(CancellationToken cancellationToken = default)
    {
        var index = await _graph.GetMajorReleaseIndexAsync(cancellationToken)
            ?? throw new InvalidOperationException("Failed to load major release index");
        return index.Embedded?.Releases?.Select(r => new ReleaseSummary(r))
            ?? Enumerable.Empty<ReleaseSummary>();
    }

    public async Task<IEnumerable<ReleaseSummary>> GetSupportedVersionsAsync(CancellationToken cancellationToken = default)
        => (await GetAllVersionsAsync(cancellationToken)).Where(r => r.IsSupported);

    public async Task<IEnumerable<ReleaseSummary>> GetVersionsByPhaseAsync(SupportPhase phase, CancellationToken cancellationToken = default)
        => (await GetAllVersionsAsync(cancellationToken)).Where(r => r.Phase == phase);

    public async Task<IEnumerable<ReleaseSummary>> GetVersionsByTypeAsync(ReleaseType type, CancellationToken cancellationToken = default)
        => (await GetAllVersionsAsync(cancellationToken)).Where(r => r.ReleaseType == type);

    public async Task<ReleaseSummary?> GetLatestAsync(CancellationToken cancellationToken = default)
        => (await GetAllVersionsAsync(cancellationToken)).FirstOrDefault();

    public async Task<ReleaseSummary?> GetLatestLtsAsync(CancellationToken cancellationToken = default)
        => (await GetVersionsByTypeAsync(ReleaseType.LTS, cancellationToken)).FirstOrDefault();

    public async Task<ReleaseSummary?> GetLatestStsAsync(CancellationToken cancellationToken = default)
        => (await GetVersionsByTypeAsync(ReleaseType.STS, cancellationToken)).FirstOrDefault();

    public async Task<ReleaseSummary?> GetLatestSupportedAsync(CancellationToken cancellationToken = default)
        => (await GetSupportedVersionsAsync(cancellationToken)).FirstOrDefault();

    public async Task<ReleaseSummary?> GetVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(version);
        return (await GetAllVersionsAsync(cancellationToken)).FirstOrDefault(r => r.Version == version);
    }

    public async Task<bool> IsSupportedAsync(string version, CancellationToken cancellationToken = default)
        => (await GetVersionAsync(version, cancellationToken))?.IsSupported ?? false;

    public ReleaseNavigator GetNavigator(string version) => new(_graph, version);
}
