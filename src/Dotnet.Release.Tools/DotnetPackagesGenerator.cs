using System.Text;
using Dotnet.Release.Support;
using Markout;
using Markout.Templates;
using MarkdownTable.Formatting;

namespace Dotnet.Release.Tools;

/// <summary>
/// Generates dotnet-packages.md from distros/ JSON files using a Markout template.
/// Shows which .NET packages are available in each distro's package feeds.
/// </summary>
public static class DotnetPackagesGenerator
{
    private const string EmbeddedTemplateName = "Dotnet.Release.Tools.dotnet-packages-template.md";

    public static MarkoutTemplate LoadTemplate(string? templatePath = null)
    {
        if (templatePath is not null)
            return MarkoutTemplate.Load(templatePath);

        var stream = typeof(DotnetPackagesGenerator).Assembly.GetManifestResourceStream(EmbeddedTemplateName)
            ?? throw new InvalidOperationException($"Embedded template not found: {EmbeddedTemplateName}");

        return MarkoutTemplate.Load(stream);
    }

    public static void ExportTemplate(TextWriter output)
    {
        using var stream = typeof(DotnetPackagesGenerator).Assembly.GetManifestResourceStream(EmbeddedTemplateName)
            ?? throw new InvalidOperationException($"Embedded template not found: {EmbeddedTemplateName}");
        using var reader = new StreamReader(stream);
        output.Write(reader.ReadToEnd());
    }

    public static void Generate(
        DistrosIndex index,
        IList<DistroPackageFile> distros,
        TextWriter output,
        string version,
        string? templatePath = null)
    {
        var template = LoadTemplate(templatePath);
        template.TableOptions = new TableFormatterOptions();

        template.Bind("version", version);
        template.Bind("summary", new SummaryBinding(distros));
        template.Bind("distros", new DistrosBinding(distros));

        var options = new MarkoutWriterOptions { PrettyTables = true };
        template.SkipUnboundPlaceholders = true;
        output.WriteLine(template.Render(options));
    }

    /// <summary>
    /// Renders a summary table showing package availability across distros.
    /// </summary>
    private class SummaryBinding(IList<DistroPackageFile> distros) : IMarkoutFormattable
    {
        public void WriteTo(MarkoutWriter writer)
        {
            writer.WriteTableStart("Distribution", "Version", "Feed");

            foreach (var distro in distros)
            {
                foreach (var release in distro.Releases)
                {
                    bool hasBuiltin = release.DotnetPackages is { Count: > 0 };
                    bool hasOther = release.DotnetPackagesOther is { Count: > 0 };

                    if (!hasBuiltin && !hasOther)
                        continue;

                    var feeds = new List<string>();
                    if (hasBuiltin)
                        feeds.Add("Built-in");
                    if (hasOther)
                        feeds.AddRange(release.DotnetPackagesOther!.Keys.Select(FormatFeedName));

                    writer.WriteTableRow(distro.Name, release.Name, string.Join(", ", feeds));
                }
            }

            writer.WriteTableEnd();
        }
    }

    /// <summary>
    /// Renders per-distro install commands for .NET packages.
    /// </summary>
    private class DistrosBinding(IList<DistroPackageFile> distros) : IMarkoutFormattable
    {
        public void WriteTo(MarkoutWriter writer)
        {
            foreach (var distro in distros)
            {
                // Skip distros with no packages on any release
                bool hasAnyPackages = distro.Releases.Any(r =>
                    r.DotnetPackages is { Count: > 0 } ||
                    r.DotnetPackagesOther is { Count: > 0 });

                if (!hasAnyPackages)
                    continue;

                writer.WriteHeading(2, distro.Name);

                foreach (var release in distro.Releases)
                {
                    bool hasBuiltin = release.DotnetPackages is { Count: > 0 };
                    bool hasOther = release.DotnetPackagesOther is { Count: > 0 };

                    if (!hasBuiltin && !hasOther)
                        continue;

                    writer.WriteHeading(3, release.Name);

                    if (hasBuiltin)
                    {
                        WriteInstallBlock(writer, distro.InstallCommand, release.DotnetPackages!);
                    }

                    if (hasOther)
                    {
                        foreach (var (feedName, feed) in release.DotnetPackagesOther!)
                        {
                            writer.WriteParagraph($"**{FormatFeedName(feedName)}:**");

                            if (!string.IsNullOrEmpty(feed.InstallCommand))
                            {
                                writer.WriteParagraph("Register the feed:");

                                writer.WriteCodeStart("bash");
                                writer.WriteParagraph(feed.InstallCommand);
                                writer.WriteCodeEnd();
                            }

                            WriteInstallBlock(writer, distro.InstallCommand, feed.Packages);
                        }
                    }

                    if (release.Notes is { Count: > 0 })
                    {
                        writer.WriteParagraph(string.Join(" ", release.Notes.Select(n => $"> {n}")));
                    }
                }
            }
        }

        private static void WriteInstallBlock(MarkoutWriter writer, string installCommand, IList<DotnetComponentPackage> packages)
        {
            writer.WriteCodeStart("bash");
            string codeBlock = BuildInstallCommand(installCommand, packages);
            writer.WriteParagraph(codeBlock);
            writer.WriteCodeEnd();
        }

        private static string BuildInstallCommand(string installCommand, IList<DotnetComponentPackage> packages)
        {
            var sb = new StringBuilder();

            string command = installCommand.Replace("{packages}", "").TrimEnd();

            if (command.StartsWith("apt-get"))
            {
                sb.AppendLine("sudo apt-get update && \\");
                sb.Append("sudo ");
            }
            else
            {
                sb.Append("sudo ");
            }

            sb.Append(command);
            sb.AppendLine(" \\");

            var sorted = packages.OrderBy(p => p.Name).ToList();
            string indent = new(' ', 4);

            for (int i = 0; i < sorted.Count; i++)
            {
                sb.Append(indent);
                sb.Append(sorted[i].Name);

                if (i + 1 < sorted.Count)
                    sb.AppendLine(" \\");
                else
                    sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }
    }

    private static string FormatFeedName(string feedName) => feedName switch
    {
        "backports" => "Backports PPA",
        "microsoft" => "Microsoft PMC",
        "core" => "Homebrew Core",
        "nixpkgs" => "Nixpkgs",
        _ => char.ToUpper(feedName[0]) + feedName[1..]
    };
}
