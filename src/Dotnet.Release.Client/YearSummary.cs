using Dotnet.Release.Graph;

namespace Dotnet.Release.Client;

/// <summary>
/// Summary of .NET release history for a specific year.
/// </summary>
public class YearSummary
{
    private readonly HistoryYearEntry _yearEntry;

    public YearSummary(HistoryYearEntry yearEntry)
    {
        ArgumentNullException.ThrowIfNull(yearEntry);
        _yearEntry = yearEntry;
    }

    public string Year => _yearEntry.Year;
    public string? Description => _yearEntry.Description;
    public IList<string>? MajorReleases => _yearEntry.MajorReleases;
    public int MajorReleaseCount => MajorReleases?.Count ?? 0;
    public IReadOnlyDictionary<string, HalLink> Links => _yearEntry.Links;
}
