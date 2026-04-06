# dotnet-release

.NET release data models and tools for consuming [dotnet/core](https://github.com/dotnet/core) release notes JSON files and the HAL+JSON release graph.

## Packages

| Package | Description |
|---|---|
| `Dotnet.Release` | Shared enums, converters, and constants |
| `Dotnet.Release.Graph` | HAL+JSON release index graph types |
| `Dotnet.Release.Releases` | Legacy releases.json schema types |
| `Dotnet.Release.Cve` | CVE disclosure schema types |
| `Dotnet.Release.Support` | OS support matrix and package types |
| `Dotnet.Release.Client` | Client library for navigating the release graph |

## Tools

Tools are published as [RID-specific NuGet tool packages](https://learn.microsoft.com/dotnet/core/tools/rid-specific-tools) with native AOT binaries for linux-x64, linux-arm64, osx-arm64, win-x64, and win-arm64. A CoreCLR fallback is included for other platforms.

Install from [GitHub Packages](https://github.com/richlander/dotnet-release/pkgs/nuget):

```bash
# Add the GitHub Packages source (using gh CLI for authentication)
dotnet nuget add source https://nuget.pkg.github.com/richlander/index.json --name richlander --username $(gh api user --jq .login) --password $(gh auth token)

# Or manually with a personal access token
dotnet nuget add source https://nuget.pkg.github.com/richlander/index.json --name richlander --username <GITHUB_USERNAME> --password <GITHUB_TOKEN>
```

Install tools globally:

```bash
dotnet tool install -g Dotnet.Release.Tool
dotnet tool install -g ReleaseNotes.Gen
```

### `dotnet-release`

Query the public release graph on the `release-index` branch.

| Command | Description |
|---|---|
| `dotnet-release` | Show a release overview from `llms.json` |
| `dotnet-release releases [--all]` | List supported or all major releases |
| `dotnet-release release <version>` | Show lifecycle and recent patches for a major release |
| `dotnet-release downloads <version> [component\|band] [--rid <rid>]` | Traverse the downloads subtree and show whether downloads are available |
| `dotnet-release timeline [period]` | Traverse the release timeline by year, month, or exact day |
| `dotnet-release skill` | Print agent guidance for answering release-graph questions |
| `dotnet-release cves [-n <months>]` | Show CVEs from the last `n` months |
| `dotnet-release cves since <date>` | Show CVEs since a given date (`YYYY`, `YYYY-MM`, or `YYYY-MM-DD`) |
| `dotnet-release cves --product <name>` | Filter CVEs by affected product |
| `dotnet-release cves --package <name>` | Filter CVEs by affected package |

Examples:

```bash
dotnet-release
dotnet-release releases
dotnet-release release 10.0
dotnet-release downloads 10.0
dotnet-release downloads 10.0 runtime --rid linux-x64
dotnet-release timeline 2026
dotnet-release timeline 2026-03
dotnet-release timeline 2026-03-10
dotnet-release cves -n 6
dotnet-release cves since 2025
dotnet-release cves since 2026-01
dotnet-release cves --product runtime -n 12
dotnet-release cves --package System.Security.Cryptography.Cose since 2026-01
```

### `release-notes-gen`

Maintainer CLI for generating markdown and index files from .NET release data.

| Subcommand | Description |
|---|---|
| `release-notes-gen generate supported-os <version>` | Generates supported-os.md from supported-os.json |
| `release-notes-gen generate os-packages <version>` | Generates os-packages.md from os-packages.json |

Options:

- `[path-or-url]` — Local path or remote URL (defaults to GitHub release-index branch)
- `--template <file>` — Use a custom template
- `--export-template` — Export the built-in template for customization

Examples:

```bash
release-notes-gen generate supported-os 10.0
release-notes-gen generate os-packages 10.0 ~/git/core/release-notes
release-notes-gen generate supported-os --export-template > my-template.md
```
