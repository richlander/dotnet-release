using System.Text.Encodings.Web;
using System.Text.Json;
using Dotnet.Release.Support;

namespace Dotnet.Release.Tools;

/// <summary>
/// Generates per-distro package files by merging os-packages.json (dependencies)
/// with distro-packages query results (dotnet package availability).
/// </summary>
public static class DistroPackageFileGenerator
{
    /// <summary>
    /// Build install command string from os-packages.json Command records.
    /// </summary>
    static string BuildInstallCommand(IList<Command> commands)
    {
        // Take the last command (the install one, not the update preamble)
        var cmd = commands.Last();
        var parts = new List<string> { cmd.CommandRoot };
        if (cmd.CommandParts is not null)
        {
            parts.AddRange(cmd.CommandParts);
        }

        string result = string.Join(" ", parts);
        // Normalize placeholder
        return result.Replace("{packageName}", "{packages}");
    }

    /// <summary>
    /// Generate per-distro JSON files from os-packages.json data,
    /// optionally enriched with distro-packages query results.
    /// </summary>
    public static IList<(string fileName, DistroPackageFile file)> Generate(
        OSPackagesOverview osPackages,
        DistroPackagesOverview? distroPackages = null)
    {
        var results = new List<(string, DistroPackageFile)>();

        foreach (var dist in osPackages.Distributions)
        {
            string fileName = dist.Name.ToLowerInvariant()
                .Replace(" ", "_");

            // Build install command
            string installCommand = BuildInstallCommand(dist.InstallCommands);

            // Build releases
            var releases = new List<DistroPackageRelease>();

            foreach (var rel in dist.Releases)
            {
                // Dependencies from os-packages.json
                var deps = rel.Packages.Select(p =>
                    new DistroDepPackage(p.Id, p.Name)).ToList();

                // Look up dotnet packages from query results
                IList<DotnetComponentPackage>? builtinPackages = null;
                Dictionary<string, DotnetAlternativeFeed>? otherFeeds = null;

                if (distroPackages is not null)
                {
                    var matchingDistro = FindMatchingDistro(distroPackages, dist.Name, rel.Release);

                    if (matchingDistro is not null)
                    {
                        foreach (var (feedName, packages) in matchingDistro)
                        {
                            var components = packages.Select(p =>
                                new DotnetComponentPackage(p.ComponentId, p.PackageName)).ToList();

                            if (feedName == "builtin")
                            {
                                builtinPackages = components;
                            }
                            else
                            {
                                otherFeeds ??= new Dictionary<string, DotnetAlternativeFeed>();
                                // Feed install commands would need to come from distro-sources.json
                                // For now, use a placeholder
                                otherFeeds[feedName] = new DotnetAlternativeFeed(
                                    InstallCommand: $"# See distro documentation for {feedName} feed setup",
                                    Packages: components);
                            }
                        }
                    }
                }

                releases.Add(new DistroPackageRelease(
                    Name: rel.Name,
                    Release: rel.Release,
                    Dependencies: deps,
                    DotnetPackages: builtinPackages,
                    DotnetPackagesOther: otherFeeds));
            }

            results.Add((fileName, new DistroPackageFile(
                Name: dist.Name,
                InstallCommand: installCommand,
                Releases: releases)));
        }

        return results;
    }

    /// <summary>
    /// Find matching distro release in query results. Handles case-insensitive
    /// and partial name matching (e.g. "Ubuntu" matches "ubuntu" or "Ubuntu").
    /// </summary>
    static IDictionary<string, IList<DotnetDistroPackage>>? FindMatchingDistro(
        DistroPackagesOverview distroPackages, string distroName, string release)
    {
        foreach (var dist in distroPackages.Distributions)
        {
            // Match by name (case-insensitive)
            if (!dist.Name.Equals(distroName, StringComparison.OrdinalIgnoreCase) &&
                !dist.Name.StartsWith(distroName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var rel in dist.Releases)
            {
                if (rel.Release == release && rel.Feeds is not null)
                {
                    return rel.Feeds;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Write per-distro JSON files to a directory.
    /// </summary>
    public static void WriteToDirectory(
        IList<(string fileName, DistroPackageFile file)> files,
        string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var writerOptions = new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        foreach (var (fileName, file) in files)
        {
            string path = Path.Combine(outputDir, $"{fileName}.json");
            using var stream = File.Create(path);
            using var writer = new Utf8JsonWriter(stream, writerOptions);
            JsonSerializer.Serialize(writer, file,
                DistroPackageFileSerializerContext.Default.DistroPackageFile);
            // Add trailing newline
            stream.WriteByte((byte)'\n');
        }
    }
}
