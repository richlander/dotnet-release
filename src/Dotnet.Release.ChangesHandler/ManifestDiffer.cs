using System.Text.Json;
using Dotnet.Release.Changes;

namespace Dotnet.Release.ChangesHandler;

/// <summary>
/// Compares two source-manifest.json files to find repos with changed commit SHAs.
/// </summary>
public static class ManifestDiffer
{
    /// <summary>
    /// Reads a source-manifest.json from a local git repo at a given ref.
    /// </summary>
    public static async Task<SourceManifest> LoadFromGitAsync(string repoPath, string gitRef)
    {
        var json = await GitHelpers.ShowFileAsync(repoPath, gitRef, "src/source-manifest.json");
        return JsonSerializer.Deserialize(json, SourceManifestSerializerContext.Default.SourceManifest)
            ?? throw new InvalidOperationException($"Failed to deserialize source-manifest.json at {gitRef}");
    }

    /// <summary>
    /// Finds repos whose commit SHAs differ between two manifests.
    /// </summary>
    public static List<RepoDiff> Diff(SourceManifest baseManifest, SourceManifest headManifest)
    {
        var baseRepos = baseManifest.Repositories.ToDictionary(r => r.Path, StringComparer.OrdinalIgnoreCase);
        var diffs = new List<RepoDiff>();

        foreach (var headRepo in headManifest.Repositories)
        {
            if (baseRepos.TryGetValue(headRepo.Path, out var baseRepo))
            {
                if (!string.Equals(baseRepo.CommitSha, headRepo.CommitSha, StringComparison.OrdinalIgnoreCase))
                {
                    var (org, repo) = ParseRemoteUri(headRepo.RemoteUri);
                    diffs.Add(new RepoDiff(headRepo.Path, org, repo, baseRepo.CommitSha, headRepo.CommitSha, headRepo.RemoteUri));
                }
            }
            else
            {
                // New repo in head — include all commits as new
                var (org, repo) = ParseRemoteUri(headRepo.RemoteUri);
                diffs.Add(new RepoDiff(headRepo.Path, org, repo, "", headRepo.CommitSha, headRepo.RemoteUri));
            }
        }

        return diffs;
    }

    private static (string Org, string Repo) ParseRemoteUri(string remoteUri)
    {
        // Handles https://github.com/dotnet/runtime and https://github.com/nuget/nuget.client
        var uri = new Uri(remoteUri);
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2)
        {
            throw new InvalidOperationException($"Cannot parse org/repo from URI: {remoteUri}");
        }
        return (segments[0], segments[1]);
    }
}

/// <summary>
/// A repo whose commit SHA changed between two manifests.
/// </summary>
public record RepoDiff(
    string Path,
    string Org,
    string Repo,
    string BaseSha,
    string HeadSha,
    string RemoteUri
);
