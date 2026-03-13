using Dotnet.Release.Support;
using Markout;

namespace Dotnet.Release.Tools;

/// <summary>
/// Verifies supported-os.json against endoflife.date and produces a Markout report.
/// Ported and modernized from distroessed ReleaseReportGenerator.
/// </summary>
public static class SupportedOsVerifier
{
    /// <summary>
    /// Verifies all distros in supported-os.json against endoflife.date lifecycle data.
    /// Returns a serializable report model.
    /// </summary>
    public static async Task<SupportedOsReport> VerifyAsync(
        SupportedOSMatrix matrix,
        HttpClient client,
        TextWriter log)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var threeMonths = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3));
        var families = new List<SupportedOsFamilyReport>();

        foreach (var family in matrix.Families)
        {
            var distros = new List<SupportedOsDistroReport>();

            foreach (var distro in family.Distributions)
            {
                log.Write($"  Checking {distro.Name} ({distro.Id})... ");

                IList<SupportCycle>? cycles;
                try
                {
                    cycles = await EndOfLifeDate.GetProductAsync(client, distro.Id);
                    if (cycles is null || cycles.Count == 0)
                    {
                        log.WriteLine("no data");
                        continue;
                    }
                    log.WriteLine($"{cycles.Count} cycles");
                }
                catch (HttpRequestException)
                {
                    log.WriteLine("not found");
                    continue;
                }

                var report = Classify(distro.Name, distro, cycles, today, threeMonths);
                if (report.HasIssues)
                    distros.Add(report);
            }

            if (distros.Count > 0)
                families.Add(new(family.Name, distros));
        }

        return new SupportedOsReport
        {
            Version = matrix.ChannelVersion,
            GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"),
            Families = families
        };
    }

    /// <summary>
    /// Classifies each distro cycle into action buckets.
    /// </summary>
    static SupportedOsDistroReport Classify(
        string name,
        SupportDistribution distro,
        IList<SupportCycle> cycles,
        DateOnly today,
        DateOnly threeMonths)
    {
        var eolButSupported = new List<CycleIssue>();
        var missing = new List<CycleIssue>();
        var activeButUnsupported = new List<CycleIssue>();
        var eolSoon = new List<CycleIssue>();

        foreach (var cycle in cycles)
        {
            if (cycle.Cycle is null) continue;

            var info = cycle.GetSupportInfo();
            bool isActive = info.IsActive;
            bool isSupported = distro.SupportedVersions.Contains(cycle.Cycle);
            bool isUnsupported = distro.UnsupportedVersions?.Contains(cycle.Cycle) ?? false;
            bool isListed = isSupported || isUnsupported;

            if (isActive && isSupported && info.EolDate < threeMonths)
                eolSoon.Add(new(cycle.Cycle, FormatEolDate(info.EolDate)));
            else if (isActive && isUnsupported)
                activeButUnsupported.Add(new(cycle.Cycle, FormatEolDate(info.EolDate)));
            else if (isActive && !isListed)
                missing.Add(new(cycle.Cycle, FormatEolDate(info.EolDate)));
            else if (!isActive && isSupported)
                eolButSupported.Add(new(cycle.Cycle, FormatEolDate(info.EolDate)));
        }

        var buckets = new List<IssueBucket>();

        if (eolButSupported.Count > 0)
            buckets.Add(new() { Alert = new(CalloutSeverity.Warning, "EOL but still listed as supported — move to unsupported-versions"), Cycles = eolButSupported });
        if (missing.Count > 0)
            buckets.Add(new() { Alert = new(CalloutSeverity.Important, "Active releases not listed — consider adding to supported-versions"), Cycles = missing });
        if (activeButUnsupported.Count > 0)
            buckets.Add(new() { Alert = new(CalloutSeverity.Tip, "Active releases listed as unsupported — verify this is intentional"), Cycles = activeButUnsupported });
        if (eolSoon.Count > 0)
            buckets.Add(new() { Alert = new(CalloutSeverity.Caution, "Supported releases reaching EOL within 3 months"), Cycles = eolSoon });

        return new SupportedOsDistroReport { Name = name, Issues = buckets };
    }

    internal static string FormatEolDate(DateOnly date) =>
        date == DateOnly.MaxValue ? "Active" :
        date == DateOnly.MinValue ? "Unknown" :
        date.ToString("yyyy-MM-dd");
}

// --- Report models ---

/// <summary>
/// Top-level verification report, serializable via Markout.
/// </summary>
[MarkoutSerializable(TitleProperty = nameof(Title))]
public class SupportedOsReport
{
    [MarkoutIgnore]
    public string Version { get; set; } = "";

    public string Title => $".NET {Version} — Supported OS Verification";

    [MarkoutPropertyName("Generated")]
    public string GeneratedAt { get; set; } = "";

    [MarkoutPropertyName("Source")]
    public string Source => "endoflife.date API";

    [MarkoutUnwrap]
    public List<SupportedOsFamilyReport> Families { get; set; } = [];

    [MarkoutIgnore]
    public bool HasIssues => Families.Count > 0;
}

[MarkoutSerializable(TitleProperty = nameof(Name))]
public class SupportedOsFamilyReport(string Name, List<SupportedOsDistroReport> Distros)
{
    [MarkoutIgnore] public string Name { get; } = Name;

    [MarkoutUnwrap]
    public List<SupportedOsDistroReport> Distros { get; } = Distros;
}

[MarkoutSerializable(TitleProperty = nameof(Name))]
public class SupportedOsDistroReport
{
    [MarkoutIgnore] public string Name { get; set; } = "";

    [MarkoutUnwrap]
    public List<IssueBucket> Issues { get; set; } = [];

    [MarkoutIgnore]
    public bool HasIssues => Issues.Count > 0;
}

/// <summary>
/// A categorized group of cycle issues with a callout describing the problem.
/// Rendered inline (no heading) — the callout provides context.
/// </summary>
[MarkoutSerializable]
public class IssueBucket
{
    [MarkoutIgnoreInTable]
    public required Callout Alert { get; init; }

    [MarkoutSection(Name = "")]
    public List<CycleIssue> Cycles { get; init; } = [];
}

[MarkoutSerializable]
public record CycleIssue(
    string Version,
    [property: MarkoutPropertyName("EOL Date")] string EolDate);

[MarkoutContext(typeof(SupportedOsReport))]
[MarkoutContext(typeof(CycleIssue))]
[MarkoutContextOptions(SuppressTableWarnings = true)]
public partial class SupportedOsReportContext : MarkoutSerializerContext { }
