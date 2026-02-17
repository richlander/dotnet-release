using Dotnet.Release.Cve;
using Dotnet.Release.Graph;

namespace Dotnet.Release.Client;

/// <summary>
/// High-level summary of .NET release history archives with CVE information.
/// </summary>
public class ArchivesSummary
{
    private readonly ReleaseNotesGraph _graph;

    public ArchivesSummary(ReleaseNotesGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
    }

    public async Task<IEnumerable<YearSummary>> GetAllYearsAsync(CancellationToken cancellationToken = default)
    {
        var historyIndex = await _graph.GetReleaseHistoryIndexAsync(cancellationToken)
            ?? throw new InvalidOperationException("Failed to load release history index");
        return historyIndex.Embedded?.Years?.Select(y => new YearSummary(y))
            ?? Enumerable.Empty<YearSummary>();
    }

    public async Task<YearSummary?> GetYearAsync(string year, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(year);
        return (await GetAllYearsAsync(cancellationToken)).FirstOrDefault(y => y.Year == year);
    }

    public async Task<YearSummary?> GetLatestYearAsync(CancellationToken cancellationToken = default)
        => (await GetAllYearsAsync(cancellationToken)).FirstOrDefault();

    public async Task<IEnumerable<CveRecords>> GetCveRecordsInDateRangeAsync(
        int startYear, int startMonth, int endYear, int endMonth,
        CancellationToken cancellationToken = default)
    {
        var allRecords = new List<CveRecords>();

        for (int year = startYear; year <= endYear; year++)
        {
            var navigator = new ArchiveNavigator(_graph, year.ToString());
            var months = await navigator.GetAllMonthsAsync(cancellationToken);

            foreach (var month in months.Where(m => m.Security))
            {
                var monthNum = int.Parse(month.Month);
                if (year == startYear && monthNum < startMonth) continue;
                if (year == endYear && monthNum > endMonth) continue;

                var records = await navigator.GetCveRecordsForMonthAsync(month.Month, cancellationToken);
                if (records is not null)
                {
                    allRecords.Add(records);
                }
            }
        }

        return allRecords;
    }

    public async Task<IEnumerable<CveRecords>> GetRecentCveRecordsAsync(
        int monthsBack,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var start = now.AddMonths(-monthsBack);
        return await GetCveRecordsInDateRangeAsync(start.Year, start.Month, now.Year, now.Month, cancellationToken);
    }

    public ArchiveNavigator GetNavigator(string year) => new(_graph, year);
}
