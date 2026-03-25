namespace Dotnet.Release.CveHandler;

/// <summary>
/// Helper for standardizing product display names.
/// </summary>
public static class ProductNameHelper
{
    public static string GetDisplayName(string productName) => productName switch
    {
        "dotnet-runtime-libraries" => ".NET Runtime Libraries",
        "dotnet-runtime-aspnetcore" => "ASP.NET Core Runtime",
        "dotnet-runtime" => ".NET Runtime Libraries",
        "dotnet-aspnetcore" => "ASP.NET Core Runtime",
        "dotnet-sdk" => ".NET SDK",
        "aspnetcore-runtime" => "ASP.NET Core Runtime",
        _ => productName
    };
}
