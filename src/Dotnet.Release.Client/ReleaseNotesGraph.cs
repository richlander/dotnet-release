using Dotnet.Release.Cve;
using Dotnet.Release.Graph;

namespace Dotnet.Release.Client;

/// <summary>
/// Provides programmatic access to the .NET release notes graph via HAL+JSON navigation.
/// </summary>
public class ReleaseNotesGraph
{
    private readonly ILinkFollower _linkFollower;
    private readonly string _baseUrl;

    public ReleaseNotesGraph(HttpClient client) : this(client, ReleaseNotes.GitHubBaseUri)
    {
    }

    public ReleaseNotesGraph(HttpClient client, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNullOrEmpty(baseUrl);
        _linkFollower = new CachingLinkFollower(client);
        _baseUrl = baseUrl;
    }

    public ReleaseNotesGraph(ILinkFollower linkFollower, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(linkFollower);
        ArgumentNullException.ThrowIfNullOrEmpty(baseUrl);
        _linkFollower = linkFollower;
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Gets the root major release version index containing all .NET versions.
    /// </summary>
    public Task<MajorReleaseVersionIndex?> GetMajorReleaseIndexAsync(CancellationToken cancellationToken = default)
        => _linkFollower.FetchAsync<MajorReleaseVersionIndex>($"{_baseUrl}index.json", cancellationToken);

    /// <summary>
    /// Gets the patch release index for a specific major version.
    /// </summary>
    public Task<PatchReleaseVersionIndex?> GetPatchReleaseIndexAsync(string version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(version);
        return _linkFollower.FetchAsync<PatchReleaseVersionIndex>($"{_baseUrl}{version}/index.json", cancellationToken);
    }

    /// <summary>
    /// Gets the manifest for a specific major version.
    /// </summary>
    public Task<ReleaseManifest?> GetManifestAsync(string version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(version);
        return _linkFollower.FetchAsync<ReleaseManifest>($"{_baseUrl}{version}/manifest.json", cancellationToken);
    }

    /// <summary>
    /// Gets the release history index (chronological view).
    /// </summary>
    public Task<ReleaseHistoryIndex?> GetReleaseHistoryIndexAsync(CancellationToken cancellationToken = default)
        => _linkFollower.FetchAsync<ReleaseHistoryIndex>($"{_baseUrl}timeline/index.json", cancellationToken);

    /// <summary>
    /// Gets the history index for a specific year.
    /// </summary>
    public Task<HistoryYearIndex?> GetYearIndexAsync(string year, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(year);
        return _linkFollower.FetchAsync<HistoryYearIndex>($"{_baseUrl}timeline/{year}/index.json", cancellationToken);
    }

    /// <summary>
    /// Gets the history index for a specific year and month.
    /// </summary>
    public Task<HistoryMonthIndex?> GetMonthIndexAsync(string year, string month, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(year);
        ArgumentNullException.ThrowIfNullOrEmpty(month);
        return _linkFollower.FetchAsync<HistoryMonthIndex>($"{_baseUrl}timeline/{year}/{month}/index.json", cancellationToken);
    }

    /// <summary>
    /// Follows a HAL link to fetch a document of the specified type.
    /// </summary>
    public Task<T?> FollowLinkAsync<T>(HalLink link, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(link);
        return _linkFollower.FetchAsync<T>(link.Href, cancellationToken);
    }

    /// <summary>
    /// Creates a high-level summary of all .NET releases.
    /// </summary>
    public ReleasesSummary GetReleasesSummary() => new(this);

    /// <summary>
    /// Creates a navigator for a specific .NET major version.
    /// </summary>
    public ReleaseNavigator GetReleaseNavigator(string version) => new(this, version);

    /// <summary>
    /// Creates a high-level summary of release history archives.
    /// </summary>
    public ArchivesSummary GetArchivesSummary() => new(this);

    /// <summary>
    /// Creates a navigator for a specific year's release history.
    /// </summary>
    public ArchiveNavigator GetArchiveNavigator(string year) => new(this, year);
}
