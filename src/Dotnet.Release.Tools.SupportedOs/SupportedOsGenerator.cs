using System.Text.Json;
using Dotnet.Release.Support;
using Markout;

namespace Dotnet.Release.Tools.SupportedOs;

/// <summary>
/// Generates supported-os.md from supported-os.json using Markout's MarkdownWriter.
/// </summary>
public static class SupportedOsGenerator
{
    public static async Task GenerateAsync(SupportedOSMatrix matrix, TextWriter output, string version, HttpClient client, string? supportPhase = null, string? releaseType = null)
    {
        var writer = new MarkdownWriter();
        int linkIndex = 0;
        List<List<string>> allFamilyLinkDefs = [];

        // Title
        writer.WriteHeading(1, $".NET {version} - Supported OS versions");
        writer.WriteParagraph($"Last Updated: {matrix.LastUpdated:yyyy/MM/dd}; Support phase: {supportPhase ?? "Unknown"}");
        writer.WriteParagraph($"[.NET {version}](README.md) is an [{releaseType ?? "Unknown"}](../../release-policies.md) release and [is supported](../../support.md) on multiple operating systems per their lifecycle policy.");

        // Family sections
        foreach (var family in matrix.Families)
        {
            List<string> familyLinkDefs = [];
            List<string> notes = [];

            writer.WriteHeading(2, family.Name);
            writer.WriteTableStart("OS", "Versions", "Architectures", "Lifecycle");

            foreach (var distro in family.Distributions)
            {
                IList<string> distroVersions = distro.Name == "Windows"
                    ? WindowsVersionHelper.SimplifyVersions(distro.SupportedVersions)
                    : distro.SupportedVersions;

                string versions = distroVersions.Count == 0
                    ? "[None](#out-of-support-os-versions)"
                    : string.Join(", ", distroVersions);

                string distroCell = $"[{distro.Name}][{linkIndex}]";
                familyLinkDefs.Add($"[{linkIndex}]: {distro.Link}");
                linkIndex++;

                string lifecycleCell = distro.Lifecycle is null
                    ? "None"
                    : $"[Lifecycle][{linkIndex}]";
                if (distro.Lifecycle is not null)
                {
                    familyLinkDefs.Add($"[{linkIndex}]: {distro.Lifecycle}");
                    linkIndex++;
                }

                writer.WriteTableRow(distroCell, versions, string.Join(", ", distro.Architectures), lifecycleCell);

                if (distro.Notes is { Count: > 0 })
                {
                    foreach (var note in distro.Notes)
                        notes.Add($"{distro.Name}: {note}");
                }
            }

            writer.WriteTableEnd();

            if (notes.Count > 0)
            {
                writer.WriteParagraph("Notes:");
                writer.WriteList(notes);
            }

            allFamilyLinkDefs.Add(familyLinkDefs);
        }

        // Libc section
        if (matrix.Libc is { Count: > 0 })
        {
            writer.WriteHeading(2, "Libc");
            writer.WriteTableStart("Libc", "Version", "Architectures", "Source");

            foreach (var libc in matrix.Libc)
                writer.WriteTableRow(libc.Name, libc.Version, string.Join(", ", libc.Architectures), libc.Source);

            writer.WriteTableEnd();
        }

        // Notes section
        if (matrix.Notes is { Count: > 0 })
        {
            writer.WriteHeading(2, "Notes");
            writer.WriteList(matrix.Notes);
        }

        // Out of support OS versions section
        await WriteUnsupportedSectionAsync(writer, matrix.Families, client);

        // Write markdown content
        output.Write(writer.ToString());

        // Append reference link definitions
        if (allFamilyLinkDefs.Any(d => d.Count > 0))
        {
            output.WriteLine();
            foreach (var familyDefs in allFamilyLinkDefs)
            {
                foreach (var def in familyDefs)
                    output.WriteLine(def);
            }
        }
    }

    private static async Task WriteUnsupportedSectionAsync(MarkdownWriter writer, IList<SupportFamily> families, HttpClient client)
    {
        // Collect all unsupported versions across families
        var unsupportedEntries = families
            .SelectMany(f => f.Distributions
                .SelectMany(d => (d.UnsupportedVersions ?? [])
                    .Select(v => (Distribution: d, Version: v))));

        if (!unsupportedEntries.Any())
            return;

        Console.Error.WriteLine("Getting EoL data...");

        // Fetch EOL data in parallel
        var eolResults = await Task.WhenAll(unsupportedEntries.Select(async entry =>
        {
            SupportCycle? cycle = null;
            try
            {
                cycle = await EndOfLifeDate.GetProductCycleAsync(client, entry.Distribution.Id, entry.Version);
            }
            catch (HttpRequestException)
            {
                Console.Error.WriteLine($"No data found at endoflife.date for: {entry.Distribution.Id} {entry.Version}");
            }
            return (entry.Distribution, entry.Version, Cycle: cycle);
        }));

        var ordered = eolResults
            .OrderBy(e => e.Distribution.Name)
            .ThenByDescending(e => e.Cycle?.GetSupportInfo().EolDate ?? DateOnly.MinValue);

        writer.WriteHeading(2, "Out of support OS versions");
        writer.WriteParagraph("OS versions that are out of support by the OS publisher are not tested or supported by .NET.");

        writer.WriteTableStart("OS", "Version", "End of Life");

        foreach (var entry in ordered)
        {
            var distroVersion = entry.Distribution.Name == "Windows"
                ? WindowsVersionHelper.Prettify(entry.Version)
                : entry.Version;

            var eolText = FormatEolDate(entry.Cycle);
            writer.WriteTableRow(entry.Distribution.Name, distroVersion, eolText);
        }

        writer.WriteTableEnd();
    }

    private static string FormatEolDate(SupportCycle? cycle)
    {
        if (cycle is null) return "-";

        var info = cycle.GetSupportInfo();
        if (info.EolDate == DateOnly.MinValue) return "-";

        var dateStr = info.EolDate == DateOnly.MaxValue ? "Active" : info.EolDate.ToString("yyyy-MM-dd");
        return cycle.Link is not null ? $"[{dateStr}]({cycle.Link})" : dateStr;
    }
}
