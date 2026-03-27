using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Dotnet.Release.Changes;

[Description("A source-manifest.json file listing repositories and their commit SHAs.")]
public record SourceManifest(
    [property: Description("The repositories in the manifest.")]
    IList<SourceManifestRepository> Repositories
);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
[Description("A repository entry in source-manifest.json.")]
public record SourceManifestRepository(
    [property: Description("Short path name, e.g. \"runtime\".")]
    string Path,

    [property: Description("Remote URI, e.g. \"https://github.com/dotnet/runtime\".")]
    string RemoteUri,

    [property: Description("Commit SHA pinned in the manifest.")]
    string CommitSha
);
