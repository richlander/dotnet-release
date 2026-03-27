namespace Dotnet.Release.ChangesHandler;

/// <summary>
/// Maps repository names to product slugs from the products.json taxonomy.
/// </summary>
public static class ProductMapper
{
    private static readonly Dictionary<string, string> RepoToProduct = new(StringComparer.OrdinalIgnoreCase)
    {
        ["runtime"] = "dotnet-runtime",
        ["aspnetcore"] = "dotnet-aspnetcore",
        ["sdk"] = "dotnet-sdk",
        ["efcore"] = "dotnet-efcore",
        ["winforms"] = "dotnet-winforms",
        ["wpf"] = "dotnet-wpf",
        ["windowsdesktop"] = "dotnet-windowsdesktop",
        ["roslyn"] = "dotnet-roslyn",
        ["fsharp"] = "dotnet-fsharp",
        ["msbuild"] = "dotnet-msbuild",
        ["nuget-client"] = "dotnet-nuget",
        ["razor"] = "dotnet-razor",
        ["templating"] = "dotnet-templating",
        ["diagnostics"] = "dotnet-diagnostics",
        ["sourcelink"] = "dotnet-sourcelink",
        ["deployment-tools"] = "dotnet-deployment-tools",
    };

    /// <summary>
    /// Returns the product slug for a repo path, or null if unmapped.
    /// </summary>
    public static string? GetProduct(string repoPath)
    {
        return RepoToProduct.TryGetValue(repoPath, out var product) ? product : null;
    }
}
