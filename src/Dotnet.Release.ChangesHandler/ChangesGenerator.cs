using System.Text.Json;
using Dotnet.Release.Changes;

namespace Dotnet.Release.ChangesHandler;

/// <summary>
/// Orchestrates the full changes pipeline: load manifests, diff, collect PRs, assemble ChangeRecords.
/// </summary>
public class ChangesGenerator(HttpClient httpClient)
{
    private readonly ChangeCollector _collector = new(httpClient);

    /// <summary>
    /// Generates a ChangeRecords from a local dotnet/dotnet clone by comparing two refs.
    /// </summary>
    public async Task<ChangeRecords> GenerateAsync(
        string repoPath,
        string baseRef,
        string headRef,
        string branch,
        string releaseVersion = "",
        string releaseDate = "",
        bool fetchLabels = false)
    {
        Console.Error.WriteLine($"Loading manifest at {baseRef}...");
        var baseManifest = await ManifestDiffer.LoadFromGitAsync(repoPath, baseRef);

        Console.Error.WriteLine($"Loading manifest at {headRef}...");
        var headManifest = await ManifestDiffer.LoadFromGitAsync(repoPath, headRef);

        var diffs = ManifestDiffer.Diff(baseManifest, headManifest);
        Console.Error.WriteLine($"Found {diffs.Count} repos with changes.");

        var changes = new List<ChangeEntry>();
        var commits = new Dictionary<string, CommitEntry>();

        foreach (var diff in diffs)
        {
            if (string.IsNullOrEmpty(diff.BaseSha))
            {
                Console.Error.WriteLine($"  {diff.Path}: new repo (skipping PR enumeration)");
                continue;
            }

            Console.Error.WriteLine($"  {diff.Path}: comparing {diff.BaseSha[..7]}...{diff.HeadSha[..7]}");

            List<PullRequestInfo> prs;
            try
            {
                prs = await _collector.GetMergedPullRequestsAsync(diff.Org, diff.Repo, diff.BaseSha, diff.HeadSha);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"    Warning: failed to collect PRs for {diff.Org}/{diff.Repo}: {ex.Message}");
                continue;
            }

            Console.Error.WriteLine($"    Found {prs.Count} PRs");

            // Fetch labels for all PRs in this repo
            Dictionary<int, IList<string>>? labelMap = null;
            if (fetchLabels && prs.Count > 0)
            {
                Console.Error.WriteLine($"    Fetching labels...");
                try
                {
                    labelMap = await _collector.GetPrLabelsAsync(
                        diff.Org, diff.Repo, prs.Select(p => p.Number));
                    Console.Error.WriteLine($"    Fetched labels for {labelMap.Count} PRs");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"    Warning: failed to fetch labels: {ex.Message}");
                }
            }

            var product = ProductMapper.GetProduct(diff.Path);

            foreach (var pr in prs)
            {
                var shortCommit = pr.MergeCommitSha[..7];
                var commitKey = $"{diff.Path}@{shortCommit}";

                if (!commits.ContainsKey(commitKey))
                {
                    commits[commitKey] = new CommitEntry(
                        Repo: diff.Path,
                        Branch: branch,
                        Hash: pr.MergeCommitSha,
                        Org: diff.Org,
                        Url: $"https://github.com/{diff.Org}/{diff.Repo}/commit/{pr.MergeCommitSha}.diff"
                    );
                }

                IList<string>? labels = null;
                labelMap?.TryGetValue(pr.Number, out labels);

                changes.Add(new ChangeEntry(
                    Id: pr.Number,
                    Repo: diff.Path,
                    Title: pr.Title,
                    Url: pr.Url,
                    Commit: commitKey,
                    IsSecurity: false,
                    Product: product,
                    Labels: labels
                ));
            }
        }

        return new ChangeRecords(
            ReleaseVersion: releaseVersion,
            ReleaseDate: releaseDate,
            Changes: changes,
            Commits: commits
        );
    }

    /// <summary>
    /// Serializes ChangeRecords to JSON and writes to a TextWriter.
    /// </summary>
    public static void Write(ChangeRecords records, TextWriter output)
    {
        var json = JsonSerializer.Serialize(records, ChangesSerializerContext.Default.ChangeRecords);
        output.Write(json);
    }

    /// <summary>
    /// Writes one ChangeRecords per repo as JSONL (one JSON object per line).
    /// </summary>
    public static void WriteJsonl(ChangeRecords records, TextWriter output)
    {
        var repoGroups = records.Changes
            .GroupBy(c => c.Repo)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in repoGroups)
        {
            var repoChanges = group.ToList();

            // Collect only commits referenced by this repo's changes
            var repoCommitKeys = repoChanges.Select(c => c.Commit)
                .Concat(repoChanges.Where(c => c.DotnetCommit is not null).Select(c => c.DotnetCommit!))
                .Distinct()
                .ToHashSet();
            var repoCommits = records.Commits
                .Where(kv => repoCommitKeys.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var repoRecord = new ChangeRecords(
                ReleaseVersion: records.ReleaseVersion,
                ReleaseDate: records.ReleaseDate,
                Changes: repoChanges,
                Commits: repoCommits
            );

            var json = JsonSerializer.Serialize(repoRecord, ChangesSerializerContext.Default.ChangeRecords);
            // JSONL: one compact JSON object per line
            output.WriteLine(json.ReplaceLineEndings(""));
        }
    }
}
