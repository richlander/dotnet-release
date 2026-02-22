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

        return new SupportedOsDistroReport(name, eolButSupported, missing, activeButUnsupported, eolSoon);
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

    [MarkoutIgnore]
    public List<SupportedOsFamilyReport> Families { get; set; } = [];

    /// <summary>
    /// Renders the nested family/distro/issue structure via IMarkoutFormattable.
    /// </summary>
    [MarkoutPropertyName("")]
    public SupportedOsReportBody Body => new(Families);

    [MarkoutIgnore]
    public bool HasIssues => Families.Count > 0;
}

public record SupportedOsFamilyReport(string Name, [property: MarkoutIgnoreInTable] List<SupportedOsDistroReport> Distros);

public record SupportedOsDistroReport(
    string Name,
    [property: MarkoutIgnoreInTable] List<CycleIssue> EolButSupported,
    [property: MarkoutIgnoreInTable] List<CycleIssue> Missing,
    [property: MarkoutIgnoreInTable] List<CycleIssue> ActiveButUnsupported,
    [property: MarkoutIgnoreInTable] List<CycleIssue> EolSoon)
{
    public bool HasIssues => EolButSupported.Count > 0 || Missing.Count > 0 ||
                             ActiveButUnsupported.Count > 0 || EolSoon.Count > 0;
}

[MarkoutSerializable]
public record CycleIssue(
    string Version,
    [property: MarkoutPropertyName("EOL Date")] string EolDate);

/// <summary>
/// Renders the body of the supported-os verification report.
/// </summary>
public class SupportedOsReportBody(List<SupportedOsFamilyReport> families) : IMarkoutFormattable
{
    public void WriteTo(MarkoutWriter writer)
    {
        if (families.Count == 0)
        {
            writer.WriteCallout(CalloutSeverity.Note, "All distributions are up to date.");
            return;
        }

        foreach (var family in families)
        {
            writer.WriteHeading(2, family.Name);

            foreach (var distro in family.Distros)
            {
                writer.WriteHeading(3, distro.Name);

                if (distro.EolButSupported.Count > 0)
                {
                    writer.WriteCallout(CalloutSeverity.Warning,
                        "EOL but still listed as supported — move to unsupported-versions");
                    WriteCycleTable(writer, distro.EolButSupported);
                }

                if (distro.Missing.Count > 0)
                {
                    writer.WriteCallout(CalloutSeverity.Important,
                        "Active releases not listed — consider adding to supported-versions");
                    WriteCycleTable(writer, distro.Missing);
                }

                if (distro.ActiveButUnsupported.Count > 0)
                {
                    writer.WriteCallout(CalloutSeverity.Tip,
                        "Active releases listed as unsupported — verify this is intentional");
                    WriteCycleTable(writer, distro.ActiveButUnsupported);
                }

                if (distro.EolSoon.Count > 0)
                {
                    writer.WriteCallout(CalloutSeverity.Caution,
                        "Supported releases reaching EOL within 3 months");
                    WriteCycleTable(writer, distro.EolSoon);
                }
            }
        }
    }

    static void WriteCycleTable(MarkoutWriter writer, List<CycleIssue> rows)
    {
        writer.WriteTableStart("Version", "EOL Date");
        foreach (var r in rows)
            writer.WriteTableRow(r.Version, r.EolDate);
        writer.WriteTableEnd();
    }
}

[MarkoutContext(typeof(SupportedOsReport))]
[MarkoutContext(typeof(CycleIssue))]
public partial class SupportedOsReportContext : MarkoutSerializerContext { }
