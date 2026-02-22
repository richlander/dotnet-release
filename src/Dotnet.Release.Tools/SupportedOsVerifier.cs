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
    /// Writes a Markout-formatted report to the output writer.
    /// </summary>
    public static async Task VerifyAsync(
        SupportedOSMatrix matrix,
        HttpClient client,
        TextWriter output,
        TextWriter log)
    {
        var writer = new MarkoutWriter(output);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var threeMonths = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3));

        writer.WriteHeading(1, $".NET {matrix.ChannelVersion} — Supported OS Verification");
        writer.WriteField("Generated", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"));
        writer.WriteField("Source", "endoflife.date API");
        writer.WriteBlankLine();

        bool hasIssues = false;

        foreach (var family in matrix.Families)
        {
            var familyIssues = new List<DistroVerification>();

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

                var verification = Classify(distro, cycles, today, threeMonths);
                if (verification.HasIssues)
                    familyIssues.Add(verification);
            }

            if (familyIssues.Count > 0)
            {
                hasIssues = true;
                writer.WriteHeading(2, family.Name);

                foreach (var v in familyIssues)
                    WriteDistroReport(writer, v);
            }
        }

        if (!hasIssues)
        {
            writer.WriteCallout(CalloutSeverity.Note, "All distributions are up to date.");
        }

        writer.Flush();
    }

    /// <summary>
    /// Classifies each distro cycle into action buckets.
    /// </summary>
    static DistroVerification Classify(
        SupportDistribution distro,
        IList<SupportCycle> cycles,
        DateOnly today,
        DateOnly threeMonths)
    {
        var result = new DistroVerification(distro.Name);

        foreach (var cycle in cycles)
        {
            var info = cycle.GetSupportInfo();
            bool isActive = info.IsActive;
            bool isSupported = distro.SupportedVersions.Contains(cycle.Cycle);
            bool isUnsupported = distro.UnsupportedVersions?.Contains(cycle.Cycle) ?? false;
            bool isListed = isSupported || isUnsupported;

            if (isActive && isSupported && info.EolDate < threeMonths)
            {
                // Active, supported, but EOL within 3 months
                result.EolSoon.Add(new(cycle.Cycle, info.EolDate));
            }
            else if (isActive && isUnsupported)
            {
                // Active in the wild but we list it as unsupported — should we add it?
                result.ActiveButUnsupported.Add(new(cycle.Cycle, info.EolDate));
            }
            else if (isActive && !isListed)
            {
                // Active release we don't list at all — might need adding
                result.Missing.Add(new(cycle.Cycle, info.EolDate, cycle.ReleaseDate));
            }
            else if (!isActive && isSupported)
            {
                // Past EOL but we still list as supported — should move to unsupported
                result.EolButSupported.Add(new(cycle.Cycle, info.EolDate));
            }
        }

        return result;
    }

    static string FormatEolDate(DateOnly date) =>
        date == DateOnly.MaxValue ? "Active" :
        date == DateOnly.MinValue ? "Unknown" :
        date.ToString("yyyy-MM-dd");

    static void WriteDistroReport(MarkoutWriter writer, DistroVerification v)
    {
        writer.WriteHeading(3, v.Name);

        if (v.EolButSupported.Count > 0)
        {
            writer.WriteCallout(CalloutSeverity.Warning, "EOL but still listed as supported — move to unsupported-versions");
            writer.WriteTableStart("Version", "EOL Date");
            foreach (var e in v.EolButSupported)
                writer.WriteTableRow(e.Cycle, FormatEolDate(e.EolDate));
            writer.WriteTableEnd();
        }

        if (v.Missing.Count > 0)
        {
            writer.WriteCallout(CalloutSeverity.Important, "Active releases not listed — consider adding to supported-versions");
            writer.WriteTableStart("Version", "EOL Date");
            foreach (var e in v.Missing)
                writer.WriteTableRow(e.Cycle, FormatEolDate(e.EolDate));
            writer.WriteTableEnd();
        }

        if (v.ActiveButUnsupported.Count > 0)
        {
            writer.WriteCallout(CalloutSeverity.Tip, "Active releases listed as unsupported — verify this is intentional");
            writer.WriteTableStart("Version", "EOL Date");
            foreach (var e in v.ActiveButUnsupported)
                writer.WriteTableRow(e.Cycle, FormatEolDate(e.EolDate));
            writer.WriteTableEnd();
        }

        if (v.EolSoon.Count > 0)
        {
            writer.WriteCallout(CalloutSeverity.Caution, "Supported releases reaching EOL within 3 months");
            writer.WriteTableStart("Version", "EOL Date");
            foreach (var e in v.EolSoon)
                writer.WriteTableRow(e.Cycle, FormatEolDate(e.EolDate));
            writer.WriteTableEnd();
        }
    }
}

record DistroVerification(string Name)
{
    public List<CycleIssue> EolButSupported { get; } = [];
    public List<CycleIssue> Missing { get; } = [];
    public List<CycleIssue> ActiveButUnsupported { get; } = [];
    public List<CycleIssue> EolSoon { get; } = [];
    public bool HasIssues => EolButSupported.Count > 0 || Missing.Count > 0 || ActiveButUnsupported.Count > 0 || EolSoon.Count > 0;
}

record CycleIssue(string Cycle, DateOnly EolDate, DateOnly ReleaseDate = default);
