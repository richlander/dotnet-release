namespace Dotnet.Release.ChangesHandler;

/// <summary>
/// Discovers preview release trains that are currently visible in a dotnet/dotnet clone.
/// </summary>
public static class ChangesPreviewQuery
{
    public static async Task<IReadOnlyList<ChangesPreviewCandidate>> FindAsync(string repoPath)
    {
        if (!Directory.Exists(repoPath))
        {
            throw new DirectoryNotFoundException($"Repo path not found: {repoPath}");
        }

        var refs = await ListCandidateRefsAsync(repoPath);
        var previews = new Dictionary<string, ChangesPreviewCandidate>(StringComparer.OrdinalIgnoreCase);
        var mainCandidates = new List<(VersionProps Props, string HeadRef)>();

        foreach (var gitRef in refs)
        {
            var props = await VersionPropsReader.ReadAsync(repoPath, gitRef);

            if (!string.Equals(props.PreReleaseLabel, "preview", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(props.PreReleaseIteration, out var iteration))
            {
                continue;
            }

            if (gitRef is "main" or "origin/main")
            {
                mainCandidates.Add((props, gitRef));
                continue;
            }

            var candidate = new ChangesPreviewCandidate(
                props.Major,
                props.Minor,
                iteration,
                props.ReleaseVersion,
                gitRef);

            if (!previews.TryGetValue(candidate.ReleaseVersion, out var existing) ||
                GetRefPriority(candidate.HeadRef) < GetRefPriority(existing.HeadRef))
            {
                previews[candidate.ReleaseVersion] = candidate;
            }
        }

        foreach (var (props, headRef) in mainCandidates)
        {
            if (!int.TryParse(props.PreReleaseIteration, out var currentIteration))
            {
                continue;
            }

            var highestBranchedIteration = previews.Values
                .Where(candidate =>
                    candidate.Major == props.Major &&
                    candidate.Minor == props.Minor &&
                    IsReleaseBranchRef(candidate.HeadRef))
                .Select(candidate => candidate.Iteration)
                .DefaultIfEmpty(0)
                .Max();

            var effectiveIteration = GetEffectiveMainIteration(currentIteration, highestBranchedIteration);
            var releaseVersion = $"{props.Major}.{props.Minor}.0-preview.{effectiveIteration}";
            var candidate = new ChangesPreviewCandidate(
                props.Major,
                props.Minor,
                effectiveIteration,
                releaseVersion,
                headRef);

            if (!previews.TryGetValue(candidate.ReleaseVersion, out var existing) ||
                GetRefPriority(candidate.HeadRef) < GetRefPriority(existing.HeadRef))
            {
                previews[candidate.ReleaseVersion] = candidate;
            }
        }

        return previews.Values
            .OrderByDescending(candidate => candidate.Major)
            .ThenByDescending(candidate => candidate.Minor)
            .ThenBy(candidate => candidate.Iteration)
            .ToList();
    }

    private static async Task<IReadOnlyList<string>> ListCandidateRefsAsync(string repoPath)
    {
        var result = await GitHelpers.RunGitAsync(
            repoPath,
            [
                "for-each-ref",
                "--format=%(refname:short)",
                "refs/heads/main",
                "refs/heads/release",
                "refs/remotes/origin/main",
                "refs/remotes/origin/release"
            ]);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git for-each-ref failed (exit {result.ExitCode}): {result.Error}");
        }

        return result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static gitRef =>
                gitRef is "main" or "origin/main" ||
                (gitRef.StartsWith("release/", StringComparison.OrdinalIgnoreCase) &&
                 gitRef.Contains("preview", StringComparison.OrdinalIgnoreCase)) ||
                (gitRef.StartsWith("origin/release/", StringComparison.OrdinalIgnoreCase) &&
                 gitRef.Contains("preview", StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GetRefPriority(string gitRef) => gitRef switch
    {
        var value when value.StartsWith("release/", StringComparison.OrdinalIgnoreCase) => 0,
        var value when value.StartsWith("origin/release/", StringComparison.OrdinalIgnoreCase) => 1,
        "main" => 2,
        "origin/main" => 3,
        _ => 4
    };

    private static bool IsReleaseBranchRef(string gitRef) =>
        gitRef.StartsWith("release/", StringComparison.OrdinalIgnoreCase) ||
        gitRef.StartsWith("origin/release/", StringComparison.OrdinalIgnoreCase);

    internal static int GetEffectiveMainIteration(int currentIteration, int highestBranchedIteration)
        => Math.Max(currentIteration, highestBranchedIteration + (highestBranchedIteration > 0 ? 1 : 0));
}

public sealed record ChangesPreviewCandidate(
    int Major,
    int Minor,
    int Iteration,
    string ReleaseVersion,
    string HeadRef);
