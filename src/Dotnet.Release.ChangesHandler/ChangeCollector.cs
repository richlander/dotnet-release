using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Dotnet.Release.ChangesHandler;

/// <summary>
/// Collects PRs and commits from a GitHub repo between two commit SHAs using the compare API.
/// </summary>
public partial class ChangeCollector(HttpClient httpClient)
{
    private const string GitHubApiBase = "https://api.github.com";
    private const int MaxPerPage = 100;

    /// <summary>
    /// Gets the PRs merged between two commits in a repo via the GitHub compare API.
    /// Returns a list of (PR number, PR title, PR URL, merge commit SHA).
    /// </summary>
    public async Task<CompareResult> GetMergedPullRequestsAsync(
        string org, string repo, string baseSha, string headSha)
    {
        var prs = new Dictionary<int, PullRequestInfo>();

        // Use compare API to get commits in range
        var commits = await GetCompareCommitsAsync(org, repo, baseSha, headSha);
        var orderedShas = commits.Select(c => c.Sha).ToList();

        foreach (var commit in commits)
        {
            // Extract PR number from merge commit messages like "Merge pull request #123 from ..."
            // or squash merge messages that contain "(#123)"
            var prNumber = ParsePrNumber(commit.Message);
            if (prNumber > 0 && !prs.ContainsKey(prNumber))
            {
                prs[prNumber] = new PullRequestInfo(
                    prNumber,
                    CleanTitle(commit.Message, prNumber),
                    $"https://github.com/{org}/{repo}/pull/{prNumber}",
                    commit.Sha
                );
            }
        }

        return new CompareResult([.. prs.Values], orderedShas);
    }

    /// <summary>
    /// Fetches labels for a batch of PRs from a single repo, with concurrency control.
    /// </summary>
    public async Task<Dictionary<int, IList<string>>> GetPrLabelsAsync(
        string org, string repo, IEnumerable<int> prNumbers, int maxConcurrency = 10)
    {
        var results = new Dictionary<int, IList<string>>();
        var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = new List<Task>();

        foreach (var prNumber in prNumbers)
        {
            await semaphore.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var labels = await GetPrLabelsAsync(org, repo, prNumber);
                    if (labels.Count > 0)
                    {
                        lock (results)
                        {
                            results[prNumber] = labels;
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        return results;
    }

    private async Task<List<string>> GetPrLabelsAsync(string org, string repo, int prNumber)
    {
        var url = $"{GitHubApiBase}/repos/{org}/{repo}/pulls/{prNumber}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await response.Content.ReadAsStreamAsync();
        var pr = await JsonSerializer.DeserializeAsync(json, GitHubSerializerContext.Default.GitHubPullRequest);
        return pr?.Labels?.Select(l => l.Name).ToList() ?? [];
    }

    private async Task<List<CompareCommit>> GetCompareCommitsAsync(
        string org, string repo, string baseSha, string headSha)
    {
        var allCommits = new List<CompareCommit>();
        int page = 1;

        while (true)
        {
            var url = $"{GitHubApiBase}/repos/{org}/{repo}/compare/{baseSha}...{headSha}?per_page={MaxPerPage}&page={page}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.Error.WriteLine($"Warning: GitHub compare API returned {response.StatusCode} for {org}/{repo}: {body}");
                break;
            }

            var json = await response.Content.ReadAsStreamAsync();
            var compareResponse = await JsonSerializer.DeserializeAsync(json, GitHubSerializerContext.Default.CompareResponse);

            if (compareResponse?.Commits is null or { Count: 0 })
            {
                break;
            }

            foreach (var commit in compareResponse.Commits)
            {
                allCommits.Add(new CompareCommit(commit.Sha, commit.Commit.Message));
            }

            // If we got fewer than the page size, we're done
            if (compareResponse.Commits.Count < MaxPerPage)
            {
                break;
            }

            page++;
        }

        return allCommits;
    }

    internal static int ParsePrNumber(string commitMessage)
    {
        // Match "Merge pull request #123 from ..."
        var mergeMatch = MergePrRegex().Match(commitMessage);
        if (mergeMatch.Success)
        {
            return int.Parse(mergeMatch.Groups[1].Value);
        }

        // Match "(#123)" in squash merge titles
        var squashMatch = SquashPrRegex().Match(commitMessage);
        if (squashMatch.Success)
        {
            return int.Parse(squashMatch.Groups[1].Value);
        }

        return 0;
    }

    internal static string CleanTitle(string commitMessage, int prNumber)
    {
        // For merge commits: "Merge pull request #123 from user/branch\n\nActual title"
        var mergeMatch = MergePrRegex().Match(commitMessage);
        if (mergeMatch.Success)
        {
            // The actual title is typically the first non-empty line after the merge commit line
            var lines = commitMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 1 ? lines[1].Trim() : "";
        }

        // For squash merges: "Fix something (#123)"
        var title = commitMessage.Split('\n')[0].Trim();
        title = SquashPrRegex().Replace(title, "").Trim();
        return title;
    }

    [GeneratedRegex(@"Merge pull request #(\d+) from")]
    private static partial Regex MergePrRegex();

    [GeneratedRegex(@"\(#(\d+)\)")]
    private static partial Regex SquashPrRegex();
}

/// <summary>
/// A PR discovered from the compare API commit range.
/// </summary>
public record PullRequestInfo(int Number, string Title, string Url, string MergeCommitSha, IList<string>? Labels = null);

/// <summary>
/// Result of comparing two commits: extracted PRs and the full ordered commit SHA list.
/// </summary>
public record CompareResult(List<PullRequestInfo> PullRequests, List<string> CommitShas);

internal record CompareCommit(string Sha, string Message);

// GitHub API response types for AOT-safe deserialization

internal record CompareResponse(
    [property: JsonPropertyName("commits")]
    IList<GitHubCommitWrapper>? Commits
);

internal record GitHubCommitWrapper(
    [property: JsonPropertyName("sha")]
    string Sha,

    [property: JsonPropertyName("commit")]
    GitHubCommitDetail Commit
);

internal record GitHubCommitDetail(
    [property: JsonPropertyName("message")]
    string Message
);

internal record GitHubLabel(
    [property: JsonPropertyName("name")]
    string Name
);

internal record GitHubPullRequest(
    [property: JsonPropertyName("labels")]
    IList<GitHubLabel>? Labels
);

[JsonSerializable(typeof(CompareResponse))]
[JsonSerializable(typeof(GitHubPullRequest))]
internal partial class GitHubSerializerContext : JsonSerializerContext
{
}
