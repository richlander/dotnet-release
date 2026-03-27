using System.Text.Json;
using Dotnet.Release.Changes;
using Dotnet.Release.Cve;

namespace Dotnet.Release.ChangesHandler;

/// <summary>
/// Cross-references changes with CVE data to identify security changes.
/// For 10.0+, CVE commits point to dotnet/dotnet (not child repos).
/// </summary>
public static class CveCrossReference
{
    /// <summary>
    /// Loads CVE data from dotnet/core release-index branch and identifies security changes.
    /// Returns a new ChangeRecords with is_security and cve_id set where applicable.
    /// </summary>
    public static async Task<ChangeRecords> ApplyAsync(
        ChangeRecords records,
        string coreRepoPath,
        string dotnetRepoPath,
        string baseRef,
        string headRef)
    {
        // Load all cve.json files from the release-index branch
        var cveData = await LoadCveFilesAsync(coreRepoPath);
        if (cveData.Count == 0)
        {
            Console.Error.WriteLine("No CVE data found on release-index branch.");
            return records;
        }

        Console.Error.WriteLine($"Loaded {cveData.Count} cve.json files from release-index branch.");

        // Collect all dotnet/dotnet security commits and their CVE associations
        var securityCommits = CollectSecurityCommits(cveData);
        if (securityCommits.Count == 0)
        {
            Console.Error.WriteLine("No dotnet/dotnet security commits found in CVE data.");
            return records;
        }

        Console.Error.WriteLine($"Found {securityCommits.Count} dotnet/dotnet security commit(s) across all CVEs.");

        // Check which security commits fall within our base..head range
        var commitsInRange = await FilterCommitsInRangeAsync(
            dotnetRepoPath, baseRef, headRef, securityCommits);

        if (commitsInRange.Count == 0)
        {
            Console.Error.WriteLine("No security commits found in the specified range.");
            return records;
        }

        Console.Error.WriteLine($"Found {commitsInRange.Count} security commit(s) in range {baseRef}..{headRef}.");

        // For each security commit in range, find what child-repo SHAs it changed
        var securityChildCommits = await MapToChildRepoCommitsAsync(
            dotnetRepoPath, commitsInRange);

        // Apply security flags to matching change entries
        return ApplySecurityFlags(records, securityChildCommits);
    }

    /// <summary>
    /// Loads all cve.json files from the release-index branch of dotnet/core.
    /// </summary>
    private static async Task<List<CveRecords>> LoadCveFilesAsync(string coreRepoPath)
    {
        var result = await GitHelpers.RunGitAsync(coreRepoPath,
            ["ls-tree", "-r", "--name-only", "release-index"]);

        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine($"Warning: could not list release-index branch: {result.Error}");
            return [];
        }

        var cveFiles = result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(f => f.EndsWith("cve.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var records = new List<CveRecords>();
        foreach (var file in cveFiles)
        {
            try
            {
                var json = await GitHelpers.ShowFileAsync(coreRepoPath, "release-index", file);
                var cveRecord = JsonSerializer.Deserialize(json, CveSerializerContext.Default.CveRecords);
                if (cveRecord is not null)
                {
                    records.Add(cveRecord);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: failed to load {file}: {ex.Message}");
            }
        }

        return records;
    }

    /// <summary>
    /// Collects all dotnet/dotnet commit hashes associated with CVEs, with their CVE IDs and product info.
    /// </summary>
    private static Dictionary<string, SecurityCommitInfo> CollectSecurityCommits(List<CveRecords> cveData)
    {
        var result = new Dictionary<string, SecurityCommitInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var cveRecord in cveData)
        {
            if (cveRecord.Commits is null || cveRecord.CveCommits is null)
            {
                continue;
            }

            foreach (var (cveId, commitKeys) in cveRecord.CveCommits)
            {
                foreach (var commitKey in commitKeys)
                {
                    if (!cveRecord.Commits.TryGetValue(commitKey, out var commitInfo))
                    {
                        continue;
                    }

                    // We want dotnet/dotnet commits (for 10.0+)
                    if (!string.Equals(commitInfo.Repo, "dotnet", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Find the product associated with this CVE+commit
                    var product = cveRecord.Products
                        .Where(p => string.Equals(p.CveId, cveId, StringComparison.OrdinalIgnoreCase)
                            && p.Commits.Contains(commitKey))
                        .Select(p => p.Name)
                        .FirstOrDefault();

                    // Find the package associated with this CVE+commit
                    var package = cveRecord.Packages
                        .Where(p => string.Equals(p.CveId, cveId, StringComparison.OrdinalIgnoreCase)
                            && p.Commits.Contains(commitKey))
                        .Select(p => p.Name)
                        .FirstOrDefault();

                    result[commitInfo.Hash] = new SecurityCommitInfo(
                        cveId, commitInfo.Hash, commitKey, product, package);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Filters commits to those that are ancestors of head but not ancestors of base.
    /// </summary>
    private static async Task<List<SecurityCommitInfo>> FilterCommitsInRangeAsync(
        string dotnetRepoPath,
        string baseRef,
        string headRef,
        Dictionary<string, SecurityCommitInfo> securityCommits)
    {
        var inRange = new List<SecurityCommitInfo>();

        foreach (var (hash, info) in securityCommits)
        {
            // Check: is this commit an ancestor of head?
            var isAncestorOfHead = await GitHelpers.RunGitAsync(dotnetRepoPath,
                ["merge-base", "--is-ancestor", hash, headRef]);

            if (isAncestorOfHead.ExitCode != 0)
            {
                continue; // Not reachable from head
            }

            // Check: is this commit an ancestor of base? If so, it was already in the previous release.
            var isAncestorOfBase = await GitHelpers.RunGitAsync(dotnetRepoPath,
                ["merge-base", "--is-ancestor", hash, baseRef]);

            if (isAncestorOfBase.ExitCode == 0)
            {
                continue; // Already in base, not new
            }

            inRange.Add(info);
        }

        return inRange;
    }

    /// <summary>
    /// For each dotnet/dotnet security commit, determines which child-repo SHAs changed
    /// by diffing source-manifest.json at the commit vs its parent.
    /// When the manifest doesn't change (direct patch to dotnet/dotnet), emits a
    /// standalone mapping with the dotnet/dotnet commit itself.
    /// </summary>
    private static async Task<List<SecurityChildMapping>> MapToChildRepoCommitsAsync(
        string dotnetRepoPath,
        List<SecurityCommitInfo> securityCommits)
    {
        var mappings = new List<SecurityChildMapping>();

        foreach (var secCommit in securityCommits)
        {
            try
            {
                // Load manifest at the security commit and its parent
                var headManifest = await ManifestDiffer.LoadFromGitAsync(dotnetRepoPath, secCommit.Hash);
                var baseManifest = await ManifestDiffer.LoadFromGitAsync(dotnetRepoPath, $"{secCommit.Hash}~1");
                var diffs = ManifestDiffer.Diff(baseManifest, headManifest);

                if (diffs.Count > 0)
                {
                    // Security fix updated child-repo SHAs in the manifest
                    foreach (var diff in diffs)
                    {
                        Console.Error.WriteLine(
                            $"    {secCommit.CveId}: {diff.Path} changed ({diff.BaseSha[..7]}→{diff.HeadSha[..7]}) via dotnet commit {secCommit.Hash[..7]}");

                        mappings.Add(new SecurityChildMapping(
                            secCommit.CveId,
                            secCommit.Hash,
                            secCommit.CommitKey,
                            diff.Path,
                            diff.HeadSha,
                            secCommit.Product,
                            secCommit.Package,
                            IsDirectPatch: false));
                    }
                }
                else
                {
                    // Direct patch to dotnet/dotnet source (no manifest change)
                    Console.Error.WriteLine(
                        $"    {secCommit.CveId}: direct patch in dotnet/dotnet commit {secCommit.Hash[..7]}");

                    mappings.Add(new SecurityChildMapping(
                        secCommit.CveId,
                        secCommit.Hash,
                        secCommit.CommitKey,
                        "dotnet",
                        secCommit.Hash,
                        secCommit.Product,
                        secCommit.Package,
                        IsDirectPatch: true));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"    Warning: could not diff manifest at {secCommit.Hash[..7]}: {ex.Message}");
            }
        }

        return mappings;
    }

    /// <summary>
    /// Applies security flags to change entries that match security child-repo commits.
    /// For direct patches (no manifest change), adds new standalone security change entries.
    /// Also adds dotnet_commit entries to the commits dictionary.
    /// </summary>
    private static ChangeRecords ApplySecurityFlags(
        ChangeRecords records,
        List<SecurityChildMapping> securityMappings)
    {
        // Build lookup: child repo full SHA → security info (for manifest-based matches)
        var securityLookup = new Dictionary<string, SecurityChildMapping>(StringComparer.OrdinalIgnoreCase);
        var directPatches = new List<SecurityChildMapping>();

        foreach (var mapping in securityMappings)
        {
            if (mapping.IsDirectPatch)
            {
                directPatches.Add(mapping);
            }
            else
            {
                securityLookup[mapping.ChildRepoSha] = mapping;
            }
        }

        var newChanges = new List<ChangeEntry>();
        var newCommits = new Dictionary<string, CommitEntry>(records.Commits);
        int matched = 0;

        // Match existing changes to security commits via child-repo SHA
        foreach (var change in records.Changes)
        {
            if (records.Commits.TryGetValue(change.Commit, out var commitEntry)
                && securityLookup.TryGetValue(commitEntry.Hash, out var secMapping))
            {
                var dotnetCommitKey = AddDotnetCommit(newCommits, secMapping, commitEntry.Branch);

                newChanges.Add(change with
                {
                    IsSecurity = true,
                    CveId = secMapping.CveId,
                    DotnetCommit = dotnetCommitKey,
                    Product = change.Product ?? secMapping.Product,
                    Package = change.Package ?? secMapping.Package
                });
                matched++;
            }
            else
            {
                newChanges.Add(change);
            }
        }

        // Add standalone entries for direct patches to dotnet/dotnet
        foreach (var directPatch in directPatches)
        {
            var dotnetCommitKey = $"dotnet@{directPatch.DotnetCommitHash[..7]}";
            if (!newCommits.ContainsKey(dotnetCommitKey))
            {
                newCommits[dotnetCommitKey] = new CommitEntry(
                    Repo: "dotnet",
                    Branch: "release/10.0",
                    Hash: directPatch.DotnetCommitHash,
                    Org: "dotnet",
                    Url: $"https://github.com/dotnet/dotnet/commit/{directPatch.DotnetCommitHash}.diff"
                );
            }

            // Security PRs are non-public; CVE data is the source of truth
            newChanges.Add(new ChangeEntry(
                Id: 0,
                Repo: "dotnet",
                Title: "",
                Url: "",
                Commit: dotnetCommitKey,
                IsSecurity: true,
                Product: directPatch.Product,
                Package: directPatch.Package,
                CveId: directPatch.CveId,
                DotnetCommit: dotnetCommitKey
            ));
            matched++;
        }

        Console.Error.WriteLine($"Matched {matched} change(s) to CVEs.");
        return new ChangeRecords(records.ReleaseVersion, records.ReleaseDate, newChanges, newCommits);
    }

    private static string AddDotnetCommit(
        Dictionary<string, CommitEntry> commits,
        SecurityChildMapping mapping,
        string branch)
    {
        var dotnetCommitKey = $"dotnet@{mapping.DotnetCommitHash[..7]}";
        if (!commits.ContainsKey(dotnetCommitKey))
        {
            commits[dotnetCommitKey] = new CommitEntry(
                Repo: "dotnet",
                Branch: branch,
                Hash: mapping.DotnetCommitHash,
                Org: "dotnet",
                Url: $"https://github.com/dotnet/dotnet/commit/{mapping.DotnetCommitHash}.diff"
            );
        }
        return dotnetCommitKey;
    }
}

internal record SecurityCommitInfo(
    string CveId,
    string Hash,
    string CommitKey,
    string? Product,
    string? Package);

internal record SecurityChildMapping(
    string CveId,
    string DotnetCommitHash,
    string DotnetCommitKey,
    string ChildRepoPath,
    string ChildRepoSha,
    string? Product,
    string? Package,
    bool IsDirectPatch);
