namespace Dotnet.Release.Client;

/// <summary>
/// Abstraction for fetching HAL+JSON documents by URL.
/// Implementations can provide caching, mocking, or other strategies.
/// </summary>
public interface ILinkFollower
{
    /// <summary>
    /// Fetches a document of type T from the specified href URL.
    /// </summary>
    Task<T?> FetchAsync<T>(string href, CancellationToken cancellationToken = default) where T : class;
}
