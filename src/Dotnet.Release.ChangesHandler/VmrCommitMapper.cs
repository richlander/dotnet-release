using Dotnet.Release.Changes;

namespace Dotnet.Release.ChangesHandler;

/// <summary>
/// Maps source-repo commit SHAs to VMR (dotnet/dotnet) commit SHAs by diffing
/// source-manifest.json at each manifest-changing VMR commit to find which
/// codeflow introduced each child-repo SHA range.
/// </summary>
public static class VmrCommitMapper
{
    /// <summary>
    /// For each source-repo commit, finds the VMR commit that brought it into dotnet/dotnet.
    /// Uses source-manifest.json diffs to build authoritative codeflow ranges, then
    /// partitions source commits using the compare API's commit ordering.
    /// </summary>
    public static async Task<Dictionary<string, string>> MapAsync(
        string repoPath,
        string baseRef,
        string headRef,
        List<RepoDiff> diffs,
        IDictionary<string, CommitEntry> sourceCommits,
        Dictionary<string, List<string>> repoCommitOrder)
    {
        // Get VMR commits that changed src/source-manifest.json, in chronological order
        var vmrManifestCommits = await GetManifestChangingCommitsAsync(repoPath, baseRef, headRef);
        if (vmrManifestCommits.Count == 0)
        {
            return new();
        }

        Console.Error.WriteLine($"  Found {vmrManifestCommits.Count} manifest-changing VMR commit(s) in range.");

        // Diff manifest at each VMR commit to build per-repo codeflow ranges
        var codeflows = await BuildCodeflowRangesAsync(repoPath, vmrManifestCommits);
        if (codeflows.Count == 0)
        {
            return new();
        }

        // Map source commits to VMR commits using codeflow ranges + commit ordering
        return MapSourceCommits(sourceCommits, codeflows, repoCommitOrder);
    }

    /// <summary>
    /// Applies the VMR commit mapping to the change entries and commits dictionary.
    /// </summary>
    public static void Apply(
        Dictionary<string, string> vmrMapping,
        List<ChangeEntry> changes,
        IDictionary<string, CommitEntry> commits,
        string branch)
    {
        foreach (var (sourceKey, vmrHash) in vmrMapping)
        {
            var vmrKey = $"dotnet@{vmrHash[..7]}";

            if (!commits.ContainsKey(vmrKey))
            {
                commits[vmrKey] = new CommitEntry(
                    Repo: "dotnet",
                    Branch: branch,
                    Hash: vmrHash,
                    Org: "dotnet",
                    Url: $"https://github.com/dotnet/dotnet/commit/{vmrHash}.diff"
                );
            }

            for (int i = 0; i < changes.Count; i++)
            {
                if (changes[i].Commit == sourceKey && changes[i].DotnetCommit is null)
                {
                    changes[i] = changes[i] with { DotnetCommit = vmrKey };
                }
            }
        }
    }

    /// <summary>
    /// Gets VMR commits that changed src/source-manifest.json, in chronological order.
    /// </summary>
    private static async Task<List<string>> GetManifestChangingCommitsAsync(
        string repoPath, string baseRef, string headRef)
    {
        var result = await GitHelpers.RunGitAsync(repoPath,
            ["log", "--format=%H", "--reverse", $"{baseRef}..{headRef}", "--", "src/source-manifest.json"]);

        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine($"  Warning: could not get VMR manifest log: {result.Error}");
            return [];
        }

        return [.. result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)];
    }

    /// <summary>
    /// For each manifest-changing VMR commit, diffs source-manifest.json at that commit
    /// vs its parent to find which repos changed and what child-repo SHA was introduced.
    /// </summary>
    private static async Task<List<CodeflowRange>> BuildCodeflowRangesAsync(
        string repoPath, List<string> vmrCommits)
    {
        var ranges = new List<CodeflowRange>();

        foreach (var vmrCommit in vmrCommits)
        {
            try
            {
                var headManifest = await ManifestDiffer.LoadFromGitAsync(repoPath, vmrCommit);
                var baseManifest = await ManifestDiffer.LoadFromGitAsync(repoPath, $"{vmrCommit}~1");
                var diffs = ManifestDiffer.Diff(baseManifest, headManifest);

                foreach (var diff in diffs)
                {
                    Console.Error.WriteLine(
                        $"    {diff.Path}: codeflow at VMR commit {vmrCommit[..7]} " +
                        $"({diff.BaseSha[..7]}→{diff.HeadSha[..7]})");
                    ranges.Add(new CodeflowRange(vmrCommit, diff.Path, diff.HeadSha));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"    Warning: could not diff manifest at {vmrCommit[..7]}: {ex.Message}");
            }
        }

        return ranges;
    }

    /// <summary>
    /// Maps source-repo commits to VMR commits using codeflow ranges and commit ordering.
    /// For repos with a single codeflow, all commits map directly.
    /// For repos with multiple codeflows, uses the compare API commit order to partition.
    /// </summary>
    private static Dictionary<string, string> MapSourceCommits(
        IDictionary<string, CommitEntry> sourceCommits,
        List<CodeflowRange> codeflows,
        Dictionary<string, List<string>> repoCommitOrder)
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var codeflowsByRepo = codeflows
            .GroupBy(c => c.RepoPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var (commitKey, entry) in sourceCommits)
        {
            if (!codeflowsByRepo.TryGetValue(entry.Repo, out var repoCodeflows))
            {
                continue;
            }

            if (repoCodeflows.Count == 1)
            {
                // Single codeflow for this repo — all source commits map to it
                mapping[commitKey] = repoCodeflows[0].VmrCommitHash;
                continue;
            }

            // Multiple codeflows — use commit ordering to find which codeflow covers this commit
            if (!repoCommitOrder.TryGetValue(entry.Repo, out var orderedShas))
            {
                continue;
            }

            // Build position index: commit SHA → position in chronological order
            var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < orderedShas.Count; i++)
            {
                positions[orderedShas[i]] = i;
            }

            if (!positions.TryGetValue(entry.Hash, out var commitPos))
            {
                continue;
            }

            // Find the first codeflow boundary that is at or after this commit's position
            var match = repoCodeflows
                .Where(cf => positions.ContainsKey(cf.NewChildSha))
                .Select(cf => (cf.VmrCommitHash, Position: positions[cf.NewChildSha]))
                .OrderBy(b => b.Position)
                .FirstOrDefault(b => b.Position >= commitPos);

            if (match != default)
            {
                mapping[commitKey] = match.VmrCommitHash;
            }
        }

        return mapping;
    }
}

internal record CodeflowRange(string VmrCommitHash, string RepoPath, string NewChildSha);
