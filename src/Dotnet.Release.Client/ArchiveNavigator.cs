using Dotnet.Release.Cve;
using Dotnet.Release.Graph;

namespace Dotnet.Release.Client;

/// <summary>
/// Deep navigation into a specific year of .NET release history.
/// Data is fetched lazily and cached automatically.
/// </summary>
public class ArchiveNavigator
{
    private readonly ReleaseNotesGraph _graph;

    public ArchiveNavigator(ReleaseNotesGraph graph, string year)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(year);
        _graph = graph;
        Year = year;
    }

    public string Year { get; }

    public async Task<HistoryYearIndex> GetYearIndexAsync(CancellationToken cancellationToken = default)
        => await _graph.GetYearIndexAsync(Year, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to load year index for {Year}");

    public async Task<IEnumerable<MonthSummary>> GetAllMonthsAsync(CancellationToken cancellationToken = default)
    {
        var index = await GetYearIndexAsync(cancellationToken);
        return index.Embedded?.Months?.Select(m => new MonthSummary(m, Year))
            ?? Enumerable.Empty<MonthSummary>();
    }

    public async Task<MonthSummary?> GetMonthAsync(string month, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(month);
        return (await GetAllMonthsAsync(cancellationToken)).FirstOrDefault(m => m.Month == month);
    }

    public async Task<IEnumerable<MonthSummary>> GetMonthsWithSecurityAsync(CancellationToken cancellationToken = default)
        => (await GetAllMonthsAsync(cancellationToken)).Where(m => m.Security);

    public async Task<CveRecords?> GetCveRecordsForMonthAsync(string month, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(month);

        var monthIndex = await _graph.GetMonthIndexAsync(Year, month, cancellationToken);
        if (monthIndex?.Links is null || !monthIndex.Links.TryGetValue(LinkRelations.CveJson, out var cveLink))
        {
            return null;
        }

        return await _graph.FollowLinkAsync<CveRecords>(cveLink, cancellationToken);
    }
}
