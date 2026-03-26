using Dotnet.Release;
using Dotnet.Release.Graph;

namespace Dotnet.Release.IndexGenerator;

public record ReleaseKindMapping(string Name, string Filename, ReleaseKind Kind, string FileType);

public record FileLink(string File, string Title, LinkStyle Style);

public record HalTuple(string Key, ReleaseKind Kind, HalLink Link);

[Flags]
public enum LinkStyle
{
    Prod = 1 << 0,
    GitHub = 1 << 1,
}
