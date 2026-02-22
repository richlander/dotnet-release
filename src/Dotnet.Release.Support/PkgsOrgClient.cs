using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Dotnet.Release.Support;

/// <summary>
/// Client for the pkgs.org API (https://pkgs.org/api/).
/// Requires Gold+ subscription. Set PKGS_ORG_TOKEN environment variable.
/// </summary>
public sealed class PkgsOrgClient : IDisposable
{
    private const string BaseUrl = "https://api.pkgs.org/v1";
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public PkgsOrgClient(string accessToken, HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient();
        _client.DefaultRequestHeaders.Add("Cookie", $"access_token={accessToken}");
    }

    /// <summary>
    /// Returns all distributions known to pkgs.org.
    /// </summary>
    public async Task<IList<PkgsOrgDistribution>> GetDistributionsAsync()
    {
        return await GetAsync($"{BaseUrl}/distributions", PkgsOrgSerializerContext.Default.IListPkgsOrgDistribution)
            ?? [];
    }

    /// <summary>
    /// Returns all repositories known to pkgs.org.
    /// </summary>
    public async Task<IList<PkgsOrgRepository>> GetRepositoriesAsync()
    {
        return await GetAsync($"{BaseUrl}/repositories", PkgsOrgSerializerContext.Default.IListPkgsOrgRepository)
            ?? [];
    }

    /// <summary>
    /// Searches for packages matching the query across all distros.
    /// </summary>
    public async Task<IList<PkgsOrgPackage>> SearchAsync(
        string query,
        bool? official = null,
        string? architecture = null,
        IEnumerable<int>? distributions = null,
        IEnumerable<int>? repositories = null)
    {
        var url = $"{BaseUrl}/search?query={Uri.EscapeDataString(query)}";

        if (official.HasValue)
            url += $"&official={official.Value.ToString().ToLowerInvariant()}";
        if (architecture is not null)
            url += $"&architecture={Uri.EscapeDataString(architecture)}";
        if (distributions is not null)
            url += $"&distributions={string.Join(",", distributions)}";
        if (repositories is not null)
            url += $"&repositories={string.Join(",", repositories)}";

        return await GetAsync(url, PkgsOrgSerializerContext.Default.IListPkgsOrgPackage)
            ?? [];
    }

    /// <summary>
    /// Searches for .NET SDK/runtime/aspnetcore packages for a given .NET major version.
    /// Returns results grouped by package name.
    /// </summary>
    public async Task<IList<PkgsOrgPackage>> SearchDotnetAsync(string dotnetMajorMinor, string? architecture = "intel")
    {
        // Search broadly for "dotnet" packages matching this version
        // Different distros use different naming: dotnet-sdk-9.0, dotnet9-sdk, etc.
        var results = new List<PkgsOrgPackage>();

        // Search for the common naming patterns
        string major = dotnetMajorMinor.Split('.')[0];
        string[] queries = [$"dotnet-sdk-{dotnetMajorMinor}", $"dotnet{major}-sdk", $"dotnet-runtime-{dotnetMajorMinor}", $"dotnet{major}-runtime"];

        foreach (var query in queries)
        {
            var packages = await SearchAsync(query, official: true, architecture: architecture);
            results.AddRange(packages);
            await Task.Delay(200); // Rate limiting
        }

        return results;
    }

    private async Task<T?> GetAsync<T>(string url, JsonTypeInfo<T> typeInfo)
    {
        using var response = await _client.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.PaymentRequired)
            throw new InvalidOperationException("pkgs.org API requires Gold+ subscription. Check your PKGS_ORG_TOKEN.");

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Invalid pkgs.org API token. Set PKGS_ORG_TOKEN environment variable.");

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(typeInfo);
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }
}
