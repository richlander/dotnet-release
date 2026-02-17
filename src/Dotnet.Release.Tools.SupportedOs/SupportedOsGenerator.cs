using System.Text.Json;
using Dotnet.Release.Support;
using Markout;

namespace Dotnet.Release.Tools.SupportedOs;

/// <summary>
/// Generates supported-os.md from supported-os.json using Markout's MarkdownWriter.
/// </summary>
public static class SupportedOsGenerator
{
    public static void Generate(SupportedOSMatrix matrix, TextWriter output, string version, string? supportPhase = null, string? releaseType = null)
    {
        var writer = new MarkdownWriter();
        int linkIndex = 0;
        // Track link definitions per-family for output after each section
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

                // Distro name link
                string distroCell = $"[{distro.Name}][{linkIndex}]";
                familyLinkDefs.Add($"[{linkIndex}]: {distro.Link}");
                linkIndex++;

                // Lifecycle link
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
                    {
                        notes.Add($"{distro.Name}: {note}");
                    }
                }
            }

            writer.WriteTableEnd();

            // Notes
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
            {
                writer.WriteTableRow(libc.Name, libc.Version, string.Join(", ", libc.Architectures), libc.Source);
            }

            writer.WriteTableEnd();
        }

        // Notes section
        if (matrix.Notes is { Count: > 0 })
        {
            writer.WriteHeading(2, "Notes");
            writer.WriteList(matrix.Notes);
        }

        // Write markdown — split by family sections and interleave link defs
        string markdown = writer.ToString();
        output.Write(markdown);

        // Append all link definitions at the end
        if (allFamilyLinkDefs.Any(d => d.Count > 0))
        {
            output.WriteLine();
            foreach (var familyDefs in allFamilyLinkDefs)
            {
                foreach (var def in familyDefs)
                {
                    output.WriteLine(def);
                }
            }
        }
    }

    /// <summary>
    /// Loads supported-os.json from a URL and generates markdown.
    /// </summary>
    public static async Task GenerateFromUrlAsync(string jsonUrl, TextWriter output, string version, string? supportPhase = null, string? releaseType = null)
    {
        using var client = new HttpClient();
        using var stream = await client.GetStreamAsync(jsonUrl);
        var matrix = await JsonSerializer.DeserializeAsync(stream, SupportedOSMatrixSerializerContext.Default.SupportedOSMatrix)
            ?? throw new InvalidOperationException("Failed to deserialize supported-os.json");
        Generate(matrix, output, version, supportPhase, releaseType);
    }
}
