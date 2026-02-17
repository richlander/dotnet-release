using System.Text;
using Dotnet.Release.Support;
using Markout;
using Markout.Templates;
using MarkdownTable.Formatting;

namespace Dotnet.Release.Tools;

/// <summary>
/// Generates os-packages.md from os-packages.json using a Markout template.
/// </summary>
public static class OsPackagesGenerator
{
    private const string EmbeddedTemplateName = "Dotnet.Release.Tools.os-packages-template.md";

    public static MarkoutTemplate LoadTemplate(string? templatePath = null)
    {
        if (templatePath is not null)
            return MarkoutTemplate.Load(templatePath);

        var stream = typeof(OsPackagesGenerator).Assembly.GetManifestResourceStream(EmbeddedTemplateName)
            ?? throw new InvalidOperationException($"Embedded template not found: {EmbeddedTemplateName}");

        return MarkoutTemplate.Load(stream);
    }

    public static void ExportTemplate(TextWriter output)
    {
        using var stream = typeof(OsPackagesGenerator).Assembly.GetManifestResourceStream(EmbeddedTemplateName)
            ?? throw new InvalidOperationException($"Embedded template not found: {EmbeddedTemplateName}");
        using var reader = new StreamReader(stream);
        output.Write(reader.ReadToEnd());
    }

    public static void Generate(OSPackagesOverview overview, TextWriter output, string version, string? templatePath = null)
    {
        var template = LoadTemplate(templatePath);
        template.TableOptions = new TableFormatterOptions();

        // Inline bindings
        template.Bind("version", version);

        // Block bindings
        var overviewBinding = new OverviewBinding(overview.Packages);
        template.Bind("overview", overviewBinding);
        template.Bind("families", new FamiliesBinding(overview.Distributions));

        // Render
        var options = new MarkoutWriterOptions { PrettyTables = true };
        template.SkipUnboundPlaceholders = true;
        output.Write(template.Render(options));

        // Append reference link definitions
        var linkDefs = overviewBinding.GetLinkDefinitions();
        if (linkDefs.Count > 0)
        {
            output.WriteLine();
            foreach (var def in linkDefs)
                output.WriteLine(def);
        }
    }

    /// <summary>
    /// Renders the package overview as a list of reference-style links.
    /// </summary>
    private class OverviewBinding(IList<Package> packages) : IMarkoutFormattable
    {
        private readonly List<string> _linkDefs = [];
        private int _linkIndex;

        public List<string> GetLinkDefinitions() => _linkDefs;

        public void WriteTo(MarkoutWriter writer)
        {
            var items = new List<string>();

            foreach (var package in packages)
            {
                string linkRef = $"[{package.Name}][{_linkIndex}]";

                // Use first reference as the link target, or pkgs.org as fallback
                string url = package.References is { Count: > 0 }
                    ? package.References[0]
                    : $"https://pkgs.org/search/?q={package.Id}";

                _linkDefs.Add($"[{_linkIndex}]: {url}");
                _linkIndex++;

                items.Add(linkRef);
            }

            writer.WriteList(items);

            // Note about globalization invariant mode
            writer.WriteParagraph("You do not need to install ICU if you [enable globalization invariant mode](https://github.com/dotnet/runtime/blob/main/docs/design/features/globalization-invariant-mode.md#enabling-the-invariant-mode).");
            writer.WriteParagraph("If your app relies on `https` endpoints, you'll also need to install `ca-certificates`.");
        }
    }

    /// <summary>
    /// Renders distribution install commands with bash code blocks.
    /// </summary>
    private class FamiliesBinding(IList<Distribution> distributions) : IMarkoutFormattable
    {
        public void WriteTo(MarkoutWriter writer)
        {
            foreach (var distro in distributions)
            {
                foreach (var release in distro.Releases)
                {
                    writer.WriteHeading(2, release.Name);

                    writer.WriteCodeStart("bash");
                    var codeBlock = BuildInstallCommand(distro.InstallCommands, release.Packages);
                    writer.WriteParagraph(codeBlock);
                    writer.WriteCodeEnd();
                }
            }
        }

        private static string BuildInstallCommand(IList<Command> commands, IList<DistroPackage> packages)
        {
            var sb = new StringBuilder();

            int commandCount = commands.Count;
            for (int i = 0; i < commandCount; i++)
            {
                var command = commands[i];
                sb.Append(GetCommandString(command));

                if (i + 1 < commandCount)
                    sb.Append(" &&");

                sb.AppendLine(" \\");
            }

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

        private static string GetCommandString(Command command)
        {
            var sb = new StringBuilder();

            if (command.RunUnderSudo)
                sb.Append("sudo ");

            sb.Append(command.CommandRoot);

            foreach (var part in command.CommandParts ?? [])
            {
                if (part == "{packageName}")
                    continue;

                sb.Append($" {part}");
            }

            return sb.ToString();
        }
    }
}
