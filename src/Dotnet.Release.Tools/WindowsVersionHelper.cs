namespace Dotnet.Release.Tools;

/// <summary>
/// Helpers for formatting Windows version strings.
/// </summary>
public static class WindowsVersionHelper
{
    /// <summary>
    /// Simplifies Windows version pairs (e.g., "2022-e" + "2022-w" → "2022").
    /// </summary>
    public static IList<string> SimplifyVersions(IList<string> versions)
    {
        List<string> updated = [];
        int prefixLen = 7;

        for (int i = 0; i < versions.Count; i++)
        {
            var version = versions[i];

            if (i + 1 < versions.Count &&
                version.Contains("-e") && versions[i + 1].Contains("-w") &&
                version.AsSpan().StartsWith(versions[i + 1].AsSpan(0, prefixLen)))
            {
                version = version.AsSpan(0, prefixLen).ToString();
                i++;
            }

            updated.Add(Prettify(version));
        }

        return updated;
    }

    /// <summary>
    /// Formats a Windows version string for display (e.g., "2022-e" → "2022 (E)").
    /// </summary>
    public static string Prettify(string version)
    {
        version = version.Replace('-', ' ').ToUpperInvariant();

        if (version.Length is 7) return version;
        if (version.Contains('W')) return $"{version.AsSpan(0, 7)} (W)";
        if (version.Contains('E')) return $"{version.AsSpan(0, 7)} (E)";
        if (version.Contains("IOT")) return $"{version.AsSpan(0, 7)} (IoT)";

        return version;
    }
}
