using System.Text;
using Dotnet.Release.Support;
using Markout;
using Markout.Templates;
using MarkdownTable.Formatting;

namespace Dotnet.Release.Tools;

/// <summary>
/// Generates dotnet-dependencies.md from distros/ JSON files using a Markout template.
/// </summary>
public static class DotnetDependenciesGenerator
{
    private const string EmbeddedTemplateName = "Dotnet.Release.Tools.dotnet-dependencies-template.md";

    public static MarkoutTemplate LoadTemplate(string? templatePath = null)
    {
        if (templatePath is not null)
            return MarkoutTemplate.Load(templatePath);

        var stream = typeof(DotnetDependenciesGenerator).Assembly.GetManifestResourceStream(EmbeddedTemplateName)
            ?? throw new InvalidOperationException($"Embedded template not found: {EmbeddedTemplateName}");

        return MarkoutTemplate.Load(stream);
    }

    public static void ExportTemplate(TextWriter output)
    {
        using var stream = typeof(DotnetDependenciesGenerator).Assembly.GetManifestResourceStream(EmbeddedTemplateName)
            ?? throw new InvalidOperationException($"Embedded template not found: {EmbeddedTemplateName}");
        using var reader = new StreamReader(stream);
        output.Write(reader.ReadToEnd());
    }

    public static void Generate(
        DependenciesFile dependencies,
        DistrosIndex index,
        IList<DistroPackageFile> distros,
        TextWriter output,
        string version,
        string? templatePath = null)
    {
        var template = LoadTemplate(templatePath);
        template.TableOptions = new TableFormatterOptions();

        template.Bind("version", version);
        template.Bind("overview", new OverviewBinding(dependencies.Packages));
        template.Bind("distros", new DistrosBinding(distros));

        var options = new MarkoutWriterOptions { PrettyTables = true };
        template.SkipUnboundPlaceholders = true;
        output.WriteLine(template.Render(options));
    }

    /// <summary>
    /// Renders the package overview as a list with links.
    /// </summary>
    private class OverviewBinding(IList<DependencyPackage> packages) : IMarkoutFormattable
    {
        public void WriteTo(MarkoutWriter writer)
        {
            var items = new List<string>();
            var linkDefs = new List<string>();
            int linkIndex = 0;

            foreach (var package in packages)
            {
                string linkRef = $"[{package.Name}][{linkIndex}]";

                string url = package.References is { Count: > 0 }
                    ? package.References[0]
                    : $"https://pkgs.org/search/?q={package.Id}";

                linkDefs.Add($"[{linkIndex}]: {url}");
                linkIndex++;

                items.Add(linkRef);
            }

            writer.WriteList(items.ToArray());

            writer.WriteParagraph("You do not need to install ICU if you [enable globalization invariant mode](https://github.com/dotnet/runtime/blob/main/docs/design/features/globalization-invariant-mode.md#enabling-the-invariant-mode).");
            writer.WriteParagraph("If your app relies on `https` endpoints, you'll also need to install `ca-certificates`.");

            writer.WriteLinkDefinitions(linkDefs.ToArray());
        }
    }

    /// <summary>
    /// Renders per-distro install commands with bash code blocks.
    /// </summary>
    private class DistrosBinding(IList<DistroPackageFile> distros) : IMarkoutFormattable
    {
        public void WriteTo(MarkoutWriter writer)
        {
            foreach (var distro in distros)
            {
                writer.WriteHeading(2, distro.Name);

                foreach (var release in distro.Releases)
                {
                    writer.WriteHeading(3, release.Name);

                    writer.WriteCodeStart("bash");
                    string codeBlock = BuildInstallCommand(distro.InstallCommand, release.Dependencies);
                    writer.WriteParagraph(codeBlock);
                    writer.WriteCodeEnd();
                }
            }
        }

        private static string BuildInstallCommand(string installCommand, IList<DistroDepPackage> dependencies)
        {
            var sb = new StringBuilder();

            // Parse the install command — add sudo prefix and handle multi-part commands
            // e.g. "apt-get install -y {packages}" or "apk add {packages}"
            string command = installCommand.Replace("{packages}", "").TrimEnd();

            // Check for update preamble patterns (apt-get needs update first)
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

            var sorted = dependencies.OrderBy(p => p.Name).ToList();
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
}
