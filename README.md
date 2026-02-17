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
# Add the GitHub Packages source (uses gh CLI for authentication)
dotnet nuget add source https://nuget.pkg.github.com/richlander/index.json --name richlander --username $(gh api user --jq .login) --password $(gh auth token)
```

Install tools globally:

```bash
dotnet tool install -g Dotnet.Release.Tools.SupportedOs
```

| Tool | Install | Description |
|---|---|---|
| `dotnet-supported-os` | `dotnet tool install -g Dotnet.Release.Tools.SupportedOs` | Generates supported-os.md from .NET release data |
