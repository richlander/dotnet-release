using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dotnet.Release.Cve;
using Dotnet.Release.Graph;

namespace Dotnet.Release.Client;

/// <summary>
/// ILinkFollower implementation that caches fetched documents by URL.
/// Thread-safe for concurrent access.
/// </summary>
public class CachingLinkFollower : ILinkFollower
{
    private readonly HttpClient _client;
    private readonly ConcurrentDictionary<string, object> _cache = new();

    public CachingLinkFollower(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public async Task<T?> FetchAsync<T>(string href, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNullOrEmpty(href);

        if (_cache.TryGetValue(href, out var cached))
        {
            return (T)cached;
        }

        var typeInfo = GetTypeInfo<T>();
        var document = await FetchDocumentAsync(href, typeInfo, cancellationToken);

        if (document is not null)
        {
            _cache[href] = document;
        }

        return document;
    }

    /// <summary>
    /// Clears all cached documents.
    /// </summary>
    public void Clear() => _cache.Clear();

    /// <summary>
    /// Attempts to remove a specific URL from the cache.
    /// </summary>
    public bool TryEvict(string href) => _cache.TryRemove(href, out _);

    private async Task<T?> FetchDocumentAsync<T>(string url, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken) where T : class
    {
        try
        {
            using var stream = await _client.GetStreamAsync(url, cancellationToken);
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static JsonTypeInfo<T> GetTypeInfo<T>() where T : class
    {
        return typeof(T).Name switch
        {
            nameof(MajorReleaseVersionIndex) => (JsonTypeInfo<T>)(object)ReleaseVersionIndexSerializerContext.Default.MajorReleaseVersionIndex,
            nameof(PatchReleaseVersionIndex) => (JsonTypeInfo<T>)(object)ReleaseVersionIndexSerializerContext.Default.PatchReleaseVersionIndex,
            nameof(PatchDetailIndex) => (JsonTypeInfo<T>)(object)ReleaseVersionIndexSerializerContext.Default.PatchDetailIndex,
            nameof(ReleaseManifest) => (JsonTypeInfo<T>)(object)ReleaseManifestSerializerContext.Default.ReleaseManifest,
            nameof(ReleaseHistoryIndex) => (JsonTypeInfo<T>)(object)ReleaseHistoryIndexSerializerContext.Default.ReleaseHistoryIndex,
            nameof(HistoryYearIndex) => (JsonTypeInfo<T>)(object)HistoryYearIndexSerializerContext.Default.HistoryYearIndex,
            nameof(HistoryMonthIndex) => (JsonTypeInfo<T>)(object)HistoryYearIndexSerializerContext.Default.HistoryMonthIndex,
            nameof(SdkVersionIndex) => (JsonTypeInfo<T>)(object)SdkVersionIndexSerializerContext.Default.SdkVersionIndex,
            nameof(DownloadsIndex) => (JsonTypeInfo<T>)(object)DownloadsIndexSerializerContext.Default.DownloadsIndex,
            nameof(TargetFrameworksIndex) => (JsonTypeInfo<T>)(object)TargetFrameworksSerializerContext.Default.TargetFrameworksIndex,
            nameof(LlmsIndex) => (JsonTypeInfo<T>)(object)LlmsIndexSerializerContext.Default.LlmsIndex,
            nameof(CveRecords) => (JsonTypeInfo<T>)(object)CveSerializerContext.Default.CveRecords,
            _ => throw new NotSupportedException($"Type {typeof(T).Name} is not supported for deserialization")
        };
    }
}
