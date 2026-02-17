# dotnet-release

.NET release data models for consuming [dotnet/core](https://github.com/dotnet/core) release notes JSON files.

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
dotnet tool install -g Dotnet.Release.Tools
```

### `dotnet-release generate`

Generates markdown files from .NET release JSON data.

| Subcommand | Description |
|---|---|
| `dotnet-release generate supported-os <version>` | Generates supported-os.md from supported-os.json |
| `dotnet-release generate os-packages <version>` | Generates os-packages.md from os-packages.json |

Options:

- `[path-or-url]` — Local path or remote URL (defaults to GitHub release-index branch)
- `--template <file>` — Use a custom template
- `--export-template` — Export the built-in template for customization

Examples:

```bash
dotnet-release generate supported-os 10.0
dotnet-release generate os-packages 10.0 ~/git/core/release-notes
dotnet-release generate supported-os --export-template > my-template.md
```
