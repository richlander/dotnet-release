# release-notes-gen

CLI tool for generating release metadata from .NET release data and the VMR (dotnet/dotnet).

## Quick Decision Tree

- **Writing release notes?** → `generate changes` to get the change log between two refs
- **Not sure which preview train is active?** → `query changes-previews` first
- **Verifying APIs against nightly builds?** → `generate build-metadata` for feed URLs and package versions
- **Updating support matrices?** → `generate supported-os` / `generate os-packages`
- **Regenerating release pages?** → `generate releases` / `generate releases-index`
- **Building index files?** → `generate indexes` for version/timeline/llms indexes

## When to Use This Tool

- Generating change logs between .NET preview or release tags
- Discovering nightly build versions and NuGet feed URLs for API verification
- Producing markdown for supported-os, os-packages, dotnet-dependencies, releases
- Cross-referencing CVE security data with code changes
- Building release index and timeline files

## Key Commands

### generate changes

Produces a structured JSON change log between two VMR refs, with per-repo commit/PR attribution, VMR commit mapping, and optional CVE cross-referencing.

```bash
release-notes-gen generate changes <repo-path> --base <ref> --head <ref> [options]
```

Options:
- `--base <ref>` — Base git ref (tag, branch, or commit)
- `--head <ref>` — Head git ref (tag, branch, or commit)
- `--branch <name>` — Branch name for commit metadata
- `--version <ver>` — Release version string for the output
- `--date <date>` — Release date (ISO 8601) for the output
- `--cve-repo <path>` — Path to dotnet/core clone (cross-references CVE data)
- `--labels` — Fetch PR labels from GitHub (requires GITHUB_TOKEN)
- `--jsonl` — Output JSONL (one repo per line) instead of single JSON
- `--output <file>` — Write output to file instead of stdout

Examples:

```bash
# Changes between two preview tags
release-notes-gen generate changes ~/git/dotnet \
  --base v11.0.0-preview.2.26159.112 \
  --head v11.0.0-preview.3.26179.102

# Changes on main with CVE cross-referencing
release-notes-gen generate changes ~/git/dotnet \
  --base v11.0.0-preview.2.26159.112 --head main \
  --branch main --cve-repo ~/git/core --output changes.json
```

### query changes-previews

Lists the preview release versions currently visible in a dotnet/dotnet clone, one per line, along with the head ref an agent can feed into `generate changes`.

```bash
release-notes-gen query changes-previews ~/git/dotnet

# Example output
11.0.0-preview.3  head=origin/release/11.0.1xx-preview3
11.0.0-preview.4  head=main
```

### generate build-metadata

Produces build metadata for API verification against nightly NuGet packages. Reads VMR version info and queries the nightly NuGet feed for latest package versions.

```bash
release-notes-gen generate build-metadata <repo-path> --base <ref> --head <ref> [--output <file>]
```

Output includes:
- Release version and preview branding
- Latest nightly build version matching the target preview
- SDK version and ci.dot.net tarball URL (with `{platform}` placeholder)
- NuGet feed URL and ref pack/standalone package versions

Example output:

```json
{
  "version": "11.0.0-preview.3",
  "base_ref": "v11.0.0-preview.2.26159.112",
  "head_ref": "release/11.0.1xx-preview3",
  "build": {
    "version": "11.0.0-preview.3.26179.102",
    "sdk_version": "11.0.100-preview.3.26179.102",
    "sdk_url": "https://ci.dot.net/public/Sdk/11.0.100-preview.3.26179.102/dotnet-sdk-11.0.100-preview.3.26179.102-{platform}.tar.gz"
  },
  "nuget": {
    "source": "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet11/nuget/v3/index.json",
    "packages": {
      "Microsoft.NETCore.App.Ref": "11.0.0-preview.3.26179.102",
      "Microsoft.AspNetCore.App.Ref": "11.0.0-preview.3.26179.102",
      "Microsoft.WindowsDesktop.App.Ref": "11.0.0-preview.3.26179.102",
      "Microsoft.EntityFrameworkCore": "11.0.0-preview.3.26118.109",
      "Microsoft.Data.Sqlite.Core": "11.0.0-preview.3.26118.109"
    }
  }
}
```

Using build metadata with dotnet-inspect for API verification:

```bash
# Read values from build-metadata.json
VER="11.0.0-preview.3.26179.102"
FEED="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet11/nuget/v3/index.json"

# Verify a runtime API exists in this preview
dnx dotnet-inspect -y -- find "*AnyNewLine*" \
  --package "Microsoft.NETCore.App.Ref@${VER}" --source "$FEED"

# Verify an ASP.NET Core API
dnx dotnet-inspect -y -- find "*Zstandard*" \
  --package "Microsoft.AspNetCore.App.Ref@${VER}" --source "$FEED"
```

### generate supported-os / os-packages / dotnet-dependencies / dotnet-packages

Generates markdown from .NET release JSON data for support matrices and dependency tables.

```bash
release-notes-gen generate <type> <version> [path-or-url] [--template <file>]
release-notes-gen generate <type> --export-template
```

### generate releases / releases-index

Generates release pages and index files from the dotnet/core release-notes tree.

```bash
release-notes-gen generate releases [path] [--template <file>]
release-notes-gen generate releases-index [path]
```

### generate indexes

Generates version, timeline, and LLMs index files.

```bash
release-notes-gen generate indexes <input-dir> [output-dir] [--url-root <url>]
release-notes-gen generate version-index <input-dir> [output-dir] [--url-root <url>]
release-notes-gen generate timeline-index <input-dir> [output-dir] [--url-root <url>]
release-notes-gen generate llms-index <input-dir> [output-dir] [--url-root <url>]
```

### verify

Validates generated release data against source JSON.

```bash
release-notes-gen verify <type> <version> [path-or-url]
release-notes-gen verify releases [version] [path] [--skip-hash]
```

### query distro-packages

Queries distro package availability for a given .NET version.

```bash
release-notes-gen query distro-packages --dotnet-version <ver> [--output <file>]
```

Requires PKGS_ORG_TOKEN environment variable.

## Environment Variables

- `GITHUB_TOKEN` — Required for `generate changes --labels` (GitHub API access)
- `PKGS_ORG_TOKEN` — Required for `query distro-packages` (pkgs.org API access)

## Installation

```bash
dotnet tool install -g ReleaseNotes.Gen --prerelease
```
