using Dotnet.Release.Graph;

namespace Dotnet.Release.Client;

/// <summary>
/// Summary of .NET release history for a specific month.
/// </summary>
public class MonthSummary
{
    private readonly HistoryMonthSummary _monthSummary;

    public MonthSummary(HistoryMonthSummary monthSummary, string year)
    {
        ArgumentNullException.ThrowIfNull(monthSummary);
        ArgumentNullException.ThrowIfNull(year);
        _monthSummary = monthSummary;
        Year = year;
    }

    public string Year { get; }
    public string Month => _monthSummary.Month;
    public string YearMonth => $"{Year}-{Month}";
    public bool Security => _monthSummary.Security;
    public IReadOnlyDictionary<string, HalLink>? Links => _monthSummary.Links;
}
