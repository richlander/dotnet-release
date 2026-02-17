using Dotnet.Release.Support;
using Markout;
using Markout.Templates;
using MarkdownTable.Formatting;

namespace Dotnet.Release.Tools.SupportedOs;

/// <summary>
/// Generates supported-os.md from supported-os.json using a Markout template.
/// </summary>
public static class SupportedOsGenerator
{
    public static async Task GenerateAsync(SupportedOSMatrix matrix, TextWriter output, string version, HttpClient client, string? supportPhase = null, string? releaseType = null)
    {
        var template = MarkoutTemplate.Load(
            Path.Combine(AppContext.BaseDirectory, "supported-os-template.md"));
        template.TableOptions = new TableFormatterOptions();

        // Inline bindings
        template.Bind("version", version);
        template.Bind("lastUpdated", matrix.LastUpdated.ToString("yyyy/MM/dd"));
        template.Bind("supportPhase", supportPhase ?? "Unknown");
        template.Bind("releaseType", releaseType ?? "Unknown");

        // Families block binding (includes H2 headings per family)
        var familiesBinding = new FamiliesBinding(matrix.Families);
        template.Bind("families", familiesBinding);

        // Libc conditional + block
        if (matrix.Libc is { Count: > 0 })
            template.Bind("libc", new LibcBinding(matrix.Libc));

        // Notes conditional + block
        if (matrix.Notes is { Count: > 0 })
            template.Bind("notes", new NotesBinding(matrix.Notes));

        // Unsupported section (requires async data fetch)
        var unsupportedBinding = await CreateUnsupportedBindingAsync(matrix.Families, client);
        if (unsupportedBinding is not null)
            template.Bind("unsupported", unsupportedBinding);

        // Render through MarkdownWriter
        var options = new MarkoutWriterOptions { PrettyTables = true };
        template.SkipUnboundPlaceholders = true;
        output.Write(template.Render(options));

        // Append reference link definitions (outside the writer pipeline)
        var linkDefs = familiesBinding.GetLinkDefinitions();
        if (linkDefs.Count > 0)
        {
            output.WriteLine();
            foreach (var def in linkDefs)
                output.WriteLine(def);
        }
    }

    private static async Task<UnsupportedBinding?> CreateUnsupportedBindingAsync(
        IList<SupportFamily> families, HttpClient client)
    {
        var unsupportedEntries = families
            .SelectMany(f => f.Distributions
                .SelectMany(d => (d.UnsupportedVersions ?? [])
                    .Select(v => (Distribution: d, Version: v))));

        if (!unsupportedEntries.Any())
            return null;

        Console.Error.WriteLine("Getting EoL data...");

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
            .ThenByDescending(e => e.Cycle?.GetSupportInfo().EolDate ?? DateOnly.MinValue)
            .ToList();

        return new UnsupportedBinding(ordered);
    }

    private static string FormatEolDate(SupportCycle? cycle)
    {
        if (cycle is null) return "-";

        var info = cycle.GetSupportInfo();
        if (info.EolDate == DateOnly.MinValue) return "-";

        var dateStr = info.EolDate == DateOnly.MaxValue ? "Active" : info.EolDate.ToString("yyyy-MM-dd");
        return cycle.Link is not null ? $"[{dateStr}]({cycle.Link})" : dateStr;
    }

    /// <summary>
    /// Renders all OS family sections with H2 headings and tables.
    /// </summary>
    private class FamiliesBinding(IList<SupportFamily> families) : IMarkoutFormattable
    {
        private readonly List<string> _linkDefs = [];

        public List<string> GetLinkDefinitions() => _linkDefs;

        public void WriteTo(MarkoutWriter writer)
        {
            int linkIndex = 0;

            foreach (var family in families)
            {
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
                    _linkDefs.Add($"[{linkIndex}]: {distro.Link}");
                    linkIndex++;

                    string lifecycleCell = distro.Lifecycle is null
                        ? "None"
                        : $"[Lifecycle][{linkIndex}]";
                    if (distro.Lifecycle is not null)
                    {
                        _linkDefs.Add($"[{linkIndex}]: {distro.Lifecycle}");
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
            }
        }
    }

    /// <summary>
    /// Renders the Libc table.
    /// </summary>
    private class LibcBinding(IList<SupportLibc> libc) : IMarkoutFormattable
    {
        public void WriteTo(MarkoutWriter writer)
        {
            writer.WriteTableStart("Libc", "Version", "Architectures", "Source");

            foreach (var entry in libc)
                writer.WriteTableRow(entry.Name, entry.Version, string.Join(", ", entry.Architectures), entry.Source);

            writer.WriteTableEnd();
        }
    }

    /// <summary>
    /// Renders notes as a bullet list.
    /// </summary>
    private class NotesBinding(IList<string> notes) : IMarkoutFormattable
    {
        public void WriteTo(MarkoutWriter writer)
        {
            writer.WriteList(notes);
        }
    }

    /// <summary>
    /// Renders the out-of-support OS table.
    /// </summary>
    private class UnsupportedBinding(
        List<(SupportDistribution Distribution, string Version, SupportCycle? Cycle)> entries)
        : IMarkoutFormattable
    {
        public void WriteTo(MarkoutWriter writer)
        {
            writer.WriteTableStart("OS", "Version", "End of Life");

            foreach (var entry in entries)
            {
                var distroVersion = entry.Distribution.Name == "Windows"
                    ? WindowsVersionHelper.Prettify(entry.Version)
                    : entry.Version;

                var eolText = FormatEolDate(entry.Cycle);
                writer.WriteTableRow(entry.Distribution.Name, distroVersion, eolText);
            }

            writer.WriteTableEnd();
        }
    }
}
