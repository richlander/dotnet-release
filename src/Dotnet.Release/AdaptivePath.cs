using System.Text;

namespace Dotnet.Release;

/// <summary>
/// Abstraction for accessing files via local paths or HTTP URLs.
/// </summary>
public interface IAdaptivePath
{
    bool CanHandlePath(string path);
    bool SupportsLocalPaths { get; }
    string Combine(params Span<string> segments);
    Task<Stream> GetStreamAsync(string uri);
}

/// <summary>
/// Factory for creating the appropriate path handler.
/// </summary>
public static class AdaptivePath
{
    public static IAdaptivePath Create(string basePath, HttpClient client) =>
        GetAdaptor(basePath, new WebPath(basePath, client), new FilePath(basePath));

    public static IAdaptivePath GetAdaptor(string basePath, params Span<IAdaptivePath> adaptors)
    {
        foreach (var adaptor in adaptors)
        {
            if (adaptor.CanHandlePath(basePath))
                return adaptor;
        }

        throw new NotSupportedException($"No adaptor found for path: {basePath}");
    }

    internal static string CombineSegments(string root, char slash, Span<string> segments)
    {
        var buffer = new StringBuilder();
        buffer.Append(root.TrimEnd(slash, '/', '\\'));

        foreach (var segment in segments)
        {
            buffer.Append(slash);
            buffer.Append(segment);
        }

        return buffer.ToString();
    }
}

/// <summary>
/// Accesses files on the local file system.
/// </summary>
public class FilePath(string basePath) : IAdaptivePath
{
    public Task<Stream> GetStreamAsync(string uri) => Task.FromResult<Stream>(File.OpenRead(uri));
    public string Combine(params Span<string> segments) => AdaptivePath.CombineSegments(basePath, Path.DirectorySeparatorChar, segments);
    public bool CanHandlePath(string path) => true;
    public bool SupportsLocalPaths => true;
}

/// <summary>
/// Accesses files via HTTP.
/// </summary>
public class WebPath(string basePath, HttpClient client) : IAdaptivePath
{
    public Task<Stream> GetStreamAsync(string uri) => client.GetStreamAsync(uri);
    public string Combine(params Span<string> segments) => AdaptivePath.CombineSegments(basePath, '/', segments);
    public bool CanHandlePath(string path) => path.StartsWith("http://") || path.StartsWith("https://");
    public bool SupportsLocalPaths => false;
}
